using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using DiffLib;
using ReactiveUI;

namespace Weingartner.ReactiveCompositeCollections
{
    public class CompositeListSubscription<T> : ReactiveObject, IDisposable
    {
        readonly ObservableAsPropertyHelper<ImmutableList<T>> _Items;

        public ImmutableList<T> Items => _Items.Value;

        public CompositeListSubscription(ICompositeList<T> list )
        {
            _Items = list.Items.ToProperty(this, p => p.Items);
        }

        public void Dispose()
        {
            _Items.Dispose();
        }
    }

    public interface ICompositeList<T> 
    {
        IObservable<ImmutableList<T>> Items { get; }

        ICompositeList<TB> Bind<TB>(Func<T, ICompositeList<TB>> f);
    }

    public static class CompositeListExtensions
    {

        public static ICompositeList<TResult> Select<TSource, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, TResult> f
            )
        {
            var r = m
                .Items
                .Select(p => p.Select(f).ToImmutableList())
                .Select(p => new CompositeSourceList<TResult>(p));
            return new CompositeSourceListSwitch<TResult>(r);
        }


        public static ICompositeList<TResult> SelectMany<TSource, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, ICompositeList<TResult>> f
            ) => m.Bind(f);

        public static ICompositeList<TResult> SelectMany<TSource, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, IEnumerable<TResult>> f
            ) => m.Bind(v=>new CompositeSourceList<TResult>(f(v)));


        public static ICompositeList<TResult> SelectMany<TSource, TICompositeList, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, ICompositeList<TICompositeList>> f
            , Func<TSource, TICompositeList, TResult> g
            ) => m.Bind(x => f(x).Bind(y =>
                                       {
                                           var data = new CompositeSourceList<TResult>();
                                           data.Source = data.Source.Add(g(x, y));
                                           return data;
                                       }));

        public static ICompositeList<TResult> SelectMany<TSource, TICompositeList, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, IEnumerable<TICompositeList>> f
            , Func<TSource, TICompositeList, TResult> g
            ) => m.Bind(x => new CompositeSourceList<TICompositeList>( f(x)).Bind(y =>
                                       {
                                           var data = new CompositeSourceList<TResult>();
                                           data.Source = data.Source.Add(g(x, y));
                                           return data;
                                       }));

        public static CompositeListSubscription<T> Subscribe<T>
            (this ICompositeList<T> @this) => new CompositeListSubscription<T>(@this);
    }

    public class CompositeList<T> : ICompositeList<T>
    {
        readonly ICompositeList<T> _Left;
        readonly ICompositeList<T> _Right;

        public CompositeList(ICompositeList<T> left,
                            ICompositeList<T> right)
        {
            _Left = left;
            _Right = right;
            Items = _Left.Items.CombineLatest
                (_Right.Items, (a,
                                b) => a.AddRange(b))
                                .Replay(1)
                                .RefCount();
        }

        public IObservable<ImmutableList<T>> Items { get; }

        public ICompositeList<TB> Bind<TB>(Func<T, ICompositeList<TB>> f)
        {
            var left = _Left.Bind(f);
            var right = _Right.Bind(f);
            return new CompositeList<TB>(left, right);
        }

    }

    public static class CompositeSourceListExtensions
    {
        public static void Add<T>
            (this CompositeSourceList<T> @this,
             T value )
        {
            @this.Source = @this.Source.Add(value);
        }
        public static void AddRange<T>
            (this CompositeSourceList<T> @this,
             IEnumerable<T> value )
        {
            @this.Source = @this.Source.AddRange(value);
        }
        public static void RemoveRange<T>
            (this CompositeSourceList<T> @this,
             IEnumerable<T> value )
        {
            @this.Source = @this.Source.RemoveRange(value);
        }

        public static void Remove<T>
            (this CompositeSourceList<T> @this,
             T value )
        {
            @this.Source = @this.Source.Remove(value);
        }

        public static ICompositeList<T> Concat<T>
            (this ICompositeList<T> @this,
             ICompositeList<T> other) => new CompositeList<T>(@this, other);

        public static ICompositeList<T> ToCompositeList<T>
            (this IObservable<ICompositeList<T>> @this) => 
            new CompositeSourceListSwitch<T>(@this);

        public static ICompositeList<T> ToCompositeList<T>
            (this IEnumerable<T> @this) => 
            new CompositeSourceList<T>(@this);

        public static ICompositeList<T> ToCompositeList<T>
            (this IObservable<IEnumerable<T>> @this) => 
            new CompositeSourceListSwitch<T>(@this.Select(s => s.ToCompositeList()));

        public static ICompositeList<T> Where<T>
            (this ICompositeList<T> @this, Func<T,bool>predicate ) => 
            @this.SelectMany(v => predicate(v) ? new[] {v}: new T[] {});

        /// <summary>
        /// A version of 'Where' where the predicate is allowed to return an
        /// IObservable of bool.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="@this"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static ICompositeList<T> Where<T>
            (this ICompositeList<T> @this, Func<T,IObservable<bool>> predicate ) => 

            @this
                .SelectMany
                    ( item=>
                        predicate(item)
                            .Select(r=>r ? new [] {item} : new T [] {})
                            .ToCompositeList()
                    );

        public static IObservable<List<DiffElement<T>>> ChangesObservable<T>(this ICompositeList<T> source, IEqualityComparer<T>comparer = null  )
        {
            return source
                .Items
                .StartWith(ImmutableList<T>.Empty)
                .Buffer(2, 1).Where(b => b.Count == 2)
                .Select(b =>
                        {
                            var sections = Diff.CalculateSections(b[0], b[1], comparer);
                            var alignment = Diff.AlignElements
                                (b[0], b[1], sections, new BasicReplaceInsertDeleteDiffElementAligner<T>());
                            return alignment.ToList();
                        });
        } 

        /// <summary>
        /// Generates an observable for items added to the list. Note
        /// that duplicates are ignored
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IObservable<ImmutableHashSet<T>> AddedToSetObservable<T>
            (this ICompositeList<T> source)
        {
            return source
                .Items
                .StartWith(ImmutableList<T>.Empty)
                .Buffer(2, 1).Where(b => b.Count == 2)
                .Select(b => b[1].Except(b[0]).ToImmutableHashSet())
                .Where(c=>c.Count>0);
        }

        /// <summary>
        /// Generates an observable for items removed from the list. Note
        /// that duplicates are ignored.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IObservable<ImmutableHashSet<T>> RemovedFromSetObservable<T>
            (this ICompositeList<T> source)
        {
            return source
                .Items
                .StartWith(ImmutableList<T>.Empty)
                .Buffer(2, 1).Where(b => b.Count == 2)
                .Select(b => b[0].Except(b[1]).ToImmutableHashSet())
                .Where(c=>c.Count>0);
        }

        #region aggregation
        public static IObservable<T> Aggregate<T>
            (
            this ICompositeList<T> source,
            Func<T, T, T> aggregator) => source.Items.Select(items => items.Aggregate(aggregator));

        public static IObservable<U> Aggregate<T,U>
            (
            this ICompositeList<T> source,
            Func<U, T, U> aggregator, U init) => source.Items.Select(items => items.Aggregate(init, aggregator));

        public static IObservable<bool> Any<T>
            (this ICompositeList<T> source,
             Func<T, bool> pred) => source.Items.Select(items => items.Any(pred));

        public static IObservable<bool> All<T>
            (this ICompositeList<T> source,
             Func<T, bool> pred) => source.Items.Select(items => items.All(pred));

        #region Sum
        public static IObservable<double> Sum<T>
            (
            this ICompositeList<T> source,
            Func<T,double> aggregator) => source.Items.Select(items => items.Sum( aggregator));

        public static IObservable<int> Sum<T>
            (
            this ICompositeList<T> source,
            Func<T,int> aggregator) => source.Items.Select(items => items.Sum( aggregator));

        public static IObservable<float> Sum<T>
            (
            this ICompositeList<T> source,
            Func<T,float> aggregator) => source.Items.Select(items => items.Sum( aggregator));
        #endregion

        #region maxmin
        public static IObservable<float> Max<T>
            (
            this ICompositeList<T> source,
            Func<T,float> aggregator) => source.Items.Select(items => items.Max( aggregator));
        public static IObservable<T> Max<T> ( this ICompositeList<T> source ) => source.Items.Select(items => items.Max());
        public static IObservable<float> Min<T>
            (
            this ICompositeList<T> source,
            Func<T,float> aggregator) => source.Items.Select(items => items.Min( aggregator));
        public static IObservable<T> Min<T> ( this ICompositeList<T> source ) => source.Items.Select(items => items.Min());
        #endregion
        #endregion
    }

    public class CompositeSourceListSwitch<T> : ICompositeList<T>
    {
        readonly IObservable<ICompositeList<T>> _Source;

        public CompositeSourceListSwitch(IObservable<ICompositeList<T>> source)
        {
            _Source = source;
            Items = _Source.Select(s => s.Items)
                .Switch()
                .Replay(1)
                .RefCount();
        }

        public IObservable<ImmutableList<T>> Items { get; }

        public ICompositeList<TB> Bind<TB>
            (Func<T, ICompositeList<TB>> f)
        {
            var r = _Source
                .Select(s => (ICompositeList<T>) s.Bind(f));
            return (ICompositeList<TB>) new CompositeSourceListSwitch<T>(r);
        }
    }

    public interface ICompositeReadOnlySourceList<T> : ICompositeList<T>
    {
        /// <summary>
        /// Get the source object
        /// </summary>
        ImmutableList<T> Source { get; }
    }


    public class CompositeSourceList<T> : ReactiveObject,  ICompositeReadOnlySourceList<T>
    {

        private ImmutableList<T> _Source;
        public ImmutableList<T> Source
        {
            get { return _Source; }
            set { this.RaiseAndSetIfChanged(ref _Source, value); }
        }

        public CompositeSourceList(ImmutableList<T> initial = null)
        {
            Source = initial ?? ImmutableList<T>.Empty;
            Items = this
                .WhenAnyValue(p => p.Source)
                .Replay(1)
                .RefCount();
        }

        public CompositeSourceList(IEnumerable<T> initial) : this(initial.ToImmutableList())
        {
        }

        public IObservable<ImmutableList<T>> Items { get; }


        public ICompositeList<TB> Bind<TB>(Func<T, ICompositeList<TB>> f)
        {

            var update = Items
                .Select
                (s => s.Count > 0 
                ? s.Select(f).Aggregate ((a, b) => new CompositeList<TB>(a, b))
                            : new CompositeSourceList<TB>());

            return new CompositeSourceListSwitch<TB>(update);
        }


    }
}
