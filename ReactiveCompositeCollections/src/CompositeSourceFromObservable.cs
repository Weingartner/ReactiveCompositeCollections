using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Weingartner.ReactiveCompositeCollections.Annotations;

namespace Weingartner.ReactiveCompositeCollections
{
    public static class ObservableExtensions
    {
        public static ICompositeList<T> ToSingletonCompositeList<T>(this IObservable<T> o) =>
            new CompositeList<T>(o.Select(ImmutableList.Create));

    }
}