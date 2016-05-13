using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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
