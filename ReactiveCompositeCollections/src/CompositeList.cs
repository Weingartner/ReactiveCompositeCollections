using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
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
            (CompositeSourceList<T> @this,
             T value )
        {
            @this.Source = @this.Source.Add(value);
        }
        public static void AddRange<T>
            (CompositeSourceList<T> @this,
             IEnumerable<T> value )
        {
            @this.Source = @this.Source.AddRange(value);
        }
        public static void RemoveRange<T>
            (CompositeSourceList<T> @this,
             IEnumerable<T> value )
        {
            @this.Source = @this.Source.RemoveRange(value);
        }

        public static void Remove<T>
            (CompositeSourceList<T> @this,
             T value )
        {
            @this.Source = @this.Source.Remove(value);
        }
        
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


    public class CompositeSourceList<T> : ReactiveObject,  ICompositeList<T>
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
