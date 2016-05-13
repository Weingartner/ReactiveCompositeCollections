# ReactiveCompositeCollections [![NuGet](https://img.shields.io/nuget/v/ReactiveCompositeCollections.svg?maxAge=2592000)]()
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
