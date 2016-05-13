# ReactiveCompositeCollections [![NuGet](https://img.shields.io/nuget/v/ReactiveCompositeCollections.svg?maxAge=2592000)](https://www.nuget.org/packages/ReactiveCompositeCollections/)
A .Net library for composing reactive collections providing monadic types to support LINQ.

## Motivation 
We often have heirachical components that maintain collections of things. Imagine a collection of 3D objects that
all produce lines. The 3D objects could be arranged in nested components. What we want is to be able to collect all
the lines in the heirarchy as a flat reactive object

    CompositeSourceList<ICompositeSourceList<Line>> nestedLines = new CompositeSourceList<ICompositeSourceList<Line>>();
    
    Widget wa = new Widget();
    Widget wb = new Widget();
    
    widgets.Add(wa.Lines);
    widgets.Add(wa.Lines);
    
    ICompositeList<Line> allLines = 
                   from lines in nestedLines
                   from line in lines
                   select line;
                   
    // Subscribe the the stream of flattened lines               
    allLines.Items.Subscribe((ImmutableList<Line> lines)=>RenderLines(lines));
    
    // or create an INPC object with a property Items
    using(var s = allLines.Subscribe()){
       RenderLines(s.Items);
    }

## Using ICompositeCollection with WPF.

To use the ICompositeCollectin you need to convert it to a ReadOnlyObservableCollection via the following method.

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

Note that the ReadOnlyObservableCollection is IDisposable so you should get rid of it when you don't need
it anymore.



