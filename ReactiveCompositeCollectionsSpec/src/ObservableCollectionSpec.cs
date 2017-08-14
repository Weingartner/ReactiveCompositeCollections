using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using ReactiveUI;
using Weingartner.ReactiveCompositeCollections;
using Xunit;

namespace Weingartner.ReactiveCompositeCollectionsSpec
{
    public class ObservableCollectionSpec
    {
        [Fact]
        public void ShouldWork()
        {
            var source0 = new CompositeSourceList<int>();
            var source1 = new CompositeSourceList<int>();

            var target = source0.Concat(source1);

            using (var observableCollection = target.CreateObservableCollection(EqualityComparer<int>.Default))
            {

                observableCollection.Count.Should().Be(0);

                source0.Add(0);
                source1.Add(5);

                observableCollection.Should().Equal(0, 5);

                source0.Add(1);
                source1.Add(6);

                observableCollection.Should().Equal(0, 1, 5, 6);

                source0.Remove(1);

                observableCollection.Should().Equal(0, 5, 6);
                source1.Remove(5);

                observableCollection.Should().Equal(0, 6);

                source0.ReplaceAt(0, 10);
                observableCollection.Should().Equal(10, 6);




                
            }
        }

        [Fact]
        public void DynamicFilterShouldWork()
        {
            var data = new CompositeSourceList<int>();
            var filters = new CompositeSourceList<Func<int,bool>>();

            // Compose a dynamic filtering system by using a CompositeSourceList
            // to hold a filter. 
            var target =
                from fn in filters
                from s in data
                where fn(s)
                select s;

            data.AddRange( new []{0,1,2,3,4,5,6,7,8,9,10} );

            // Convert to an observable collection for testing purposes
            using (var oc = target.CreateObservableCollection( EqualityComparer<int>.Default ))
            {
                // Set the filter to be everything greater than 5
                filters.Add(  v => v> 5 );
                oc.Should().Equal( 6, 7, 8, 9, 10 );

                // Set the filter to be everything less than 5
                filters.ReplaceAt( 0, v=>v<5 );
                oc.Should().Equal( 0, 1, 2, 3, 4 );
            }
        }

        public class FilterHolder : ReactiveObject
        {
            private Func<int,bool> _Filter;
            public Func<int,bool> Filter { get => _Filter; set => this.RaiseAndSetIfChanged( ref _Filter, value ); }
        }


        [Fact]
        public void DynamicFilterWithRxUIShouldWork()
        {
            var sourceList0 = new CompositeSourceList<int>();
            var sourceList1 = new CompositeSourceList<int>();

            var filter = new FilterHolder {Filter = v => v > 5};

            // Compose a dynamic filtering system by using a CompositeSourceList
            // to hold a filter. 
            var target =
                from fn in filter.WhenAnyValue( p=>p.Filter )
                from s in new [] {sourceList0,  sourceList1 }.Concat()
                where fn(s)
                select s;

            sourceList0.AddRange( new []{0,1,2,3,4,5,6,7,8,9,10} );

            // Convert to an observable collection for testing purposes
            using (var oc = target.CreateObservableCollection( EqualityComparer<int>.Default ))
            {
                // Set the filter to be everything greater than 5
                oc.Should().Equal( 6, 7, 8, 9, 10 );
                sourceList0.Clear();
                oc.Should().Equal();

                sourceList0.AddRange( new []{0,1} );
                sourceList1.AddRange( new []{9,10} );

                oc.Should().Equal( 9, 10);

                // Set the filter to be everything less than 5
                filter.Filter =  v=>v<5;
                oc.Should().Equal( 0, 1);
            }
        }

        [Fact]
        public void ShouldWorkWithRangeOperators()
        {
            var source0 = new CompositeSourceList<int>();
            var source1 = new CompositeSourceList<int>();

            var target = source0.Concat(source1);

            using (var observableCollection = target.CreateObservableCollection(EqualityComparer<int>.Default))
            {

                observableCollection.Count.Should().Be(0);

                source0.AddRange(new [] {0,1,2});

                observableCollection.Should().Equal(0, 1,2);

                source0.Add(1);
                source1.Add(6);

                observableCollection.Should().Equal(0, 1, 2, 1, 6);

                source1.AddRange(new [] {3,4,5});

                observableCollection.Should().Equal(0, 1, 2, 1, 6, 3, 4, 5);

                source0.InsertRangeAt(1, new [] {6,7});

                observableCollection.Should().Equal(0, 6, 7, 1, 2, 1, 6, 3, 4, 5);

                source1.InsertRangeAt(1, new [] {6,7});
                observableCollection.Should().Equal(0, 6, 7, 1, 2, 1, 6, 6, 7, 3, 4, 5);

                source0.Source.Should().Equal(0, 6, 7, 1, 2,1);
                source0.Replace(1,99);
                observableCollection.Should().Equal(0, 6, 7, 99, 2, 1, 6, 6, 7, 3, 4, 5);


            }
        }

    }
}
