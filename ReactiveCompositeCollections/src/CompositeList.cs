using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using DiffLib;
using Weingartner.ReactiveCompositeCollections.Annotations;

namespace Weingartner.ReactiveCompositeCollections
{
    public class CompositeListSubscription<T> : INotifyPropertyChanged, IDisposable
    {

        private ImmutableList<T> _Items = ImmutableList<T>.Empty;
        private IDisposable _Subscription;

        public ImmutableList<T> Items
        {
            get { return _Items; }
            set
            {
                if(_Items!=value)
                    OnPropertyChanged();
                _Items = value;
            }
        }

        public CompositeListSubscription(ICompositeList<T> list )
        {
            _Subscription = list.Items
                .Where(v=>v!=null)
                .Subscribe(v=>Items=v);

        }

        public void Dispose()
        {
            _Subscription.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged
            ([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                .Select(p => new CompositeList<TResult>(Observable.Return(p, Scheduler.Immediate)));
            return r.ToCompositeList();
        }



        public static ICompositeList<TResult> SelectMany<TSource, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, ICompositeList<TResult>> f
            ) => m.Bind(f);

        public static ICompositeList<TResult> SelectMany<TSource, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, IEnumerable<TResult>> f
            ) => m.Bind(v=>new CompositeList<TResult>(f(v)));


        public static ICompositeList<TResult> SelectMany<TSource, TICompositeList, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, ICompositeList<TICompositeList>> f
            , Func<TSource, TICompositeList, TResult> g
            ) => m.Bind(x => f(x).Bind(y => new CompositeList<TResult>(g(x,y))));

        public static ICompositeList<TResult> SelectMany<TSource, TICompositeList, TResult>
            ( this IObservable<TSource> m
            , Func<TSource, ICompositeList<TICompositeList>> f
            , Func<TSource, TICompositeList, TResult> g
            ) => m.ToSingletonCompositeList().Bind(x => f(x).Bind(y => new CompositeList<TResult>(g(x,y))));

        public static ICompositeList<TResult> SelectMany<TSource, TICompositeList, TResult>
            ( this ICompositeList<TSource> m
            , Func<TSource, IEnumerable<TICompositeList>> f
            , Func<TSource, TICompositeList, TResult> g
            ) => m.Bind(x => new CompositeList<TICompositeList>(f(x)).Bind(y => new CompositeList<TResult>(g(x, y))));

        public static CompositeListSubscription<T> Subscribe<T>
            (this ICompositeList<T> @this) => new CompositeListSubscription<T>(@this);

        public static ICompositeList<T> Take<T>(this ICompositeList<T> source, int n) => 
            source.Items.Select( v => v.Take( n ) ).ToCompositeList();
    }

    public class CompositeList<T> : ICompositeList<T>
    {
        public CompositeList(IObservable<ImmutableList<T>> source )
        {
            Items = source;
        }

        public CompositeList(ImmutableList<T> source) : this(Observable.Return(source, Scheduler.Immediate)) { }
        public CompositeList(IEnumerable<T> source) : this(Observable.Return(source.ToImmutableList(), Scheduler.Immediate)) { }

        public CompositeList(T source) : this(ImmutableList.Create(source))
        {
        }

        public IObservable<ImmutableList<T>> Items { get;  }

         public ICompositeList<TB> Bind<TB>(Func<T, ICompositeList<TB>> f)
        {

            var update = Items
                .Select(items0 =>
                        {
                            var items = items0.Select(v => f(v).Items);
                            if (!items.Any())
                                return Observable.Return(ImmutableList<TB>.Empty, Scheduler.Immediate);

                            // We use Aggregate here because internally AddRange checks to
                            // see if the passed in Enumerable is an ImmutableList and then
                            // performs an optimisation.
                            return items.CombineLatest
                                (list => list.Aggregate
                                             (ImmutableList<TB>.Empty, (a,
                                                                        b) => a.AddRange(b)));
                        })
                .Switch();

            return new CompositeList<TB>(update);

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

        /// <summary>
        /// Replaces the first equal element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="this"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        public static void Replace<T>
            (this CompositeSourceList<T> @this,
             T oldValue, T newValue )
        {
            @this.Source = @this.Source.Replace(oldValue, newValue);
        }

        public static void ReplaceAt<T>
            (this CompositeSourceList<T> @this,
             int i, T newValue )
        {
            @this.Source = @this.Source.SetItem(i, newValue);
        }
        public static void InsertAt<T>
            (this CompositeSourceList<T> @this,
             int i, T newValue )
        {
            @this.Source = @this.Source.Insert(i, newValue);
        }
        public static void InsertRangeAt<T>
            (this CompositeSourceList<T> @this,
             int i, IEnumerable<T> newValue )
        {
            @this.Source = @this.Source.InsertRange(i, newValue);
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

        public static void Clear<T>
            (this CompositeSourceList<T> @this )
        {
            @this.Source = ImmutableList<T>.Empty;
        }

        public static void Remove<T>
            (this CompositeSourceList<T> @this,
             T value )
        {
            @this.Source = @this.Source.Remove(value);
        }

        public static IDisposable Bind<T>
            (this ICompositeList<T> @this,
             CompositeSourceList<T> target) =>
            @this.Items.Subscribe(items => target.Source = items);

        public static ICompositeList<T> Concat<T>
            (this ICompositeList<T> @this,
             ICompositeList<T> other) => @this.Items.CombineLatest(other.Items,(a,b)=>a.Concat(b)).ToCompositeList();

        public static ICompositeList<T> Concat<T>(this IEnumerable<ICompositeList<T>> @this)
        {
            return @this.Aggregate( (a, b) => a.Concat( b ) );
        }

        public static ICompositeList<T> ToCompositeList<T>
            (this IObservable<ICompositeList<T>> @this) =>
                new CompositeList<T>(@this.Select(list => list.Items).Switch()); 

        public static ICompositeList<T> ToCompositeList<T>
            (this IEnumerable<T> @this) => 
            new CompositeSourceList<T>(@this);

        public static ICompositeList<T> ToCompositeList<T>
            (this IObservable<IEnumerable<T>> @this) => 
            new CompositeList<T>(@this.Select(t=>t.ToImmutableList())); 

        public static ICompositeList<T> ToCompositeList<T>
            (this IObservable<ImmutableList<T>> @this) => 
            new CompositeList<T>(@this); 

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

        public static ICompositeList<T> Where<T>
            (this ICompositeList<T> @this, IObservable<Func<T, bool>> predicateObservable)
        {
            return @this
                .Items
                .CombineLatest(predicateObservable, (items, predicate) => items.Where(predicate))
                .ToCompositeList();
        }

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

    public interface ICompositeReadOnlySourceList<T> : ICompositeList<T>
    {
        /// <summary>
        /// Get the source object
        /// </summary>
        ImmutableList<T> Source { get; }
    }


    public class CompositeSourceList<T> : INotifyPropertyChanged, ICompositeReadOnlySourceList<T>
    {

        private ImmutableList<T> _Source;
        private readonly CompositeList<T> _Bridge; 
        public ImmutableList<T> Source
        {
            get { return _Source; }
            set
            {
                if (_Source == value)
                    return;
                _Source = value;
                OnPropertyChanged();
                _SourceObserver.OnNext(value);
            }
        }

        private readonly BehaviorSubject<ImmutableList<T>> _SourceObserver; 


        public CompositeSourceList(ImmutableList<T> initial = null) 
        {
            _SourceObserver = new BehaviorSubject<ImmutableList<T>>(initial);
            Source = initial ?? ImmutableList<T>.Empty;
            _Bridge = new CompositeList<T>(_SourceObserver);
            Items = _Bridge.Items;
        }

        public CompositeSourceList(IEnumerable<T> initial) : this(initial.ToImmutableList())
        {
        }

        public IObservable<ImmutableList<T>> Items { get; }


        public ICompositeList<TB> Bind<TB>(Func<T, ICompositeList<TB>> f) => _Bridge.Bind(f);
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged
            ([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
