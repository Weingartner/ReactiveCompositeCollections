using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;

namespace Weingartner.ReactiveCompositeCollections
{
    public class CompositeSetSubscription<T> : ReactiveObject, IDisposable
    {
        readonly ObservableAsPropertyHelper<ImmutableHashSet<T>> _Items;

        public ImmutableHashSet<T> Items => _Items.Value;

        public CompositeSetSubscription(ICompositeSet<T> list )
        {
            _Items = list.Items.ToProperty(this, p => p.Items);
        }

        public void Dispose()
        {
            _Items.Dispose();
        }
    }
    public interface ICompositeSet<T> 
    {
        IObservable<ImmutableHashSet<T>> Items { get; }

        ICompositeSet<TB> Bind<TB>(Func<T, ICompositeSet<TB>> f);
    }

    public static class CompositeSetExtensions
    {

        public static ICompositeSet<TResult> Select<TSource, TResult>
            ( this ICompositeSet<TSource> m
            , Func<TSource, TResult> f
            ) => m.Bind(x =>
                        {
                            var data = new CompositeSourceSet<TResult>();
                            data.Add(f(x));
                            return data;
                        });

        public static ICompositeSet<TResult> SelectMany<TSource, TResult>
            ( this ICompositeSet<TSource> m
            , Func<TSource, ICompositeSet<TResult>> f
            ) => m.Bind(f);

        public static ICompositeSet<TResult> SelectMany<TSource, TICompositeSet, TResult>
            ( this ICompositeSet<TSource> m
            , Func<TSource, ICompositeSet<TICompositeSet>> f
            , Func<TSource, TICompositeSet, TResult> g
            ) => m.Bind(x => f(x).Bind(y =>
                                       {
                                           var data = new CompositeSourceSet<TResult>();
                                           data.Add(g(x, y));
                                           return data;
                                       }));

        public static CompositeSetSubscription<T> Subscribe<T>
            (this ICompositeSet<T> @this) => new CompositeSetSubscription<T>(@this);
    }

    public class CompositeSet<T> : ICompositeSet<T>
    {
        readonly ICompositeSet<T> _Left;
        readonly ICompositeSet<T> _Right;

        public CompositeSet(ICompositeSet<T> left,
                            ICompositeSet<T> right)
        {
            _Left = left;
            _Right = right;

            Items = _Left.Items.CombineLatest
                (_Right.Items, (a,
                                b) => a.Union(b))
                                .Replay(1)
                                .RefCount();


        }

        public IObservable<ImmutableHashSet<T>> Items { get; }



        public ICompositeSet<TB> Bind<TB>(Func<T, ICompositeSet<TB>> f)
        {
            var left = _Left.Bind(f);
            var right = _Right.Bind(f);
            return new CompositeSet<TB>(left, right);
        }

    }

    public class CompositeSourceSetSwitch<T> : ICompositeSet<T>
    {
        readonly IObservable<ICompositeSet<T>> _Source;

        public CompositeSourceSetSwitch(IObservable<ICompositeSet<T>> source)
        {
            _Source = source;
            Items = _Source.Select(s => s.Items)
                .Switch()
                .Replay(1)
                .RefCount();
        }

        public IObservable<ImmutableHashSet<T>> Items { get; }


        public ICompositeSet<TB> Bind<TB>
            (Func<T, ICompositeSet<TB>> f)
        {
            var r = _Source
                .Select(s => (ICompositeSet<T>) s.Bind(f));
            return (ICompositeSet<TB>) new CompositeSourceSetSwitch<T>(r);
        }
    }


    public class CompositeSourceSet<T> : ReactiveObject,  ICompositeSet<T>
    {

        private ImmutableHashSet<T> _Source;
        private ImmutableHashSet<T> Source
        {
            get { return _Source; }
            set { this.RaiseAndSetIfChanged(ref _Source, value); }
        }

        public CompositeSourceSet(ImmutableHashSet<T> initial = null )
        {
            Source = initial ?? ImmutableHashSet<T>.Empty;
            Items = this
                .WhenAnyValue(p => p.Source)
                .Replay(1)
                .RefCount();
        }

        public IObservable<ImmutableHashSet<T>> Items { get; }


        public ICompositeSet<TB> Bind<TB>(Func<T, ICompositeSet<TB>> f)
        {

            var update = this.WhenAnyValue(p => p.Source)
                .Select
                (s => s.Count > 0 
                ? s.Select(f).Aggregate ((a, b) => new CompositeSet<TB>(a, b))
                            : new CompositeSourceSet<TB>());

            return new CompositeSourceSetSwitch<TB>(update);
        }


        public IEnumerator<T> GetEnumerator()
        {
            return Source.GetEnumerator();
        }



        public void UnionWith
            (IEnumerable<T> other)
        {
            Source = Source.Union(other);
        }

        public void IntersectWith
            (IEnumerable<T> other)
        {
            Source = Source.Intersect(other);
        }

        public void ExceptWith
            (IEnumerable<T> other)
        {
            Source = Source.Except(other);
        }

        public void SymmetricExceptWith
            (IEnumerable<T> other)
        {
            Source = Source.SymmetricExcept(other);
        }

        public bool IsSubsetOf
            (IEnumerable<T> other)
        {
            return Source.IsSubsetOf(other);
        }

        public bool IsSupersetOf
            (IEnumerable<T> other)
        {
            return Source.IsSupersetOf(other);
        }

        public bool IsProperSupersetOf
            (IEnumerable<T> other)
        {
            return Source.IsProperSupersetOf(other);
        }

        public bool IsProperSubsetOf
            (IEnumerable<T> other)
        {
            return Source.IsProperSubsetOf(other);
        }

        public bool Overlaps
            (IEnumerable<T> other)
        {
            return Source.Overlaps(other);
        }

        public bool SetEquals
            (IEnumerable<T> other)
        {
            return Source.SetEquals(other);
        }

        public bool Add
            (T item)
        {
            if (Source.Contains(item))
                return false;
            Source = Source.Add(item);
            return true;
        }
        public void AddRange
            (IEnumerable<T> item)
        {
            Source = Source.Union(item);
        }

        public void Clear()
        {
            Source = ImmutableHashSet<T>.Empty;
        }

        public bool Contains
            (T item)
        {
            return Source.Contains(item);
        }

        public void CopyTo
            (T[] array,
             int arrayIndex)
        {
            Source.ToList().CopyTo(array, arrayIndex);
        }

        public bool Remove
            (T item)
        {
            if (!Source.Contains(item))
                return false;
            Source = Source.Remove(item);
            return true;
        }

        public int Count => Source.Count;

        public bool IsReadOnly => true;
    }
}
