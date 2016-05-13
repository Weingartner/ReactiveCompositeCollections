using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DiffLib;
using Weingartner.ReactiveCompositeCollections.Annotations;

namespace Weingartner.ReactiveCompositeCollections
{
    public class ReadOnlyObservableCollection<T> : System.Collections.ObjectModel.ReadOnlyObservableCollection<T>, IDisposable
    {
        private readonly ICompositeList<T> _List;
        private readonly System.Collections.ObjectModel.ObservableCollection<T> _Collection;
        private readonly IDisposable _Disposable;

        internal ReadOnlyObservableCollection(ICompositeList<T> list, System.Collections.ObjectModel.ObservableCollection<T> collection, IEqualityComparer<T> eq) : base(collection)
        {
            _List = list;
            _Collection = collection;

            _Disposable = list.ChangesObservable(eq)
                .Subscribe(change =>
                {
                    int i = 0;
                    foreach (var diff in change)
                    {
                        switch (diff.Operation)
                        {
                            case DiffOperation.Match:
                                break;
                            case DiffOperation.Insert:
                                _Collection.Insert(i, diff.ElementFromCollection2.Value);
                                break;
                            case DiffOperation.Delete:
                                _Collection.RemoveAt(i);
                                i--;
                                break;
                            case DiffOperation.Replace:
                                _Collection[i] = diff.ElementFromCollection2.Value;
                                break;
                            case DiffOperation.Modify:
                                _Collection[i] = diff.ElementFromCollection2.Value;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        i++;
                    }
                });
        }


        public void Dispose()
        {
            _Disposable.Dispose();
        }
    }

    public static class ObservableCollection
    {
        /// <summary>
        /// Use this to supply a ReadOnly ObservableCollection for view lists etc.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="eq"></param>
        /// <returns></returns>
        public static ReadOnlyObservableCollection<T> CreateObservableCollection<T>(this ICompositeList<T> list, IEqualityComparer<T> eq) {
            var collection = new System.Collections.ObjectModel.ObservableCollection<T>();
            return new ReadOnlyObservableCollection<T>(list, collection, eq);
        }
    }
}
