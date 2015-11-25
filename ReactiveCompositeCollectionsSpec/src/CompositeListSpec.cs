using System;
using FluentAssertions;
using ReactiveUI;
using Weingartner.ReactiveCompositeCollections;
using Xunit;

namespace Weingartner.ReactiveCompositeCollectionsSpec
{
    public class CompositeListSpec
    {
        [Fact]
        public void ManuallyNesting()
        {
            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();
            var c = new CompositeSourceList<int>();

            var x = new CompositeList<int>(a,b);
            var y = new CompositeList<int>(x,c);

            using (var s = y.Subscribe())
            {
                s.Items.Should().BeEmpty();
                a.Add(1);
                s.Items.ShouldBeEquivalentTo(new[]{1});
                b.Add(1);
                s.Items.Should().BeEquivalentTo(new[]{1,1});
                b.Add(3);
                s.Items.Should().BeEquivalentTo(new[]{1,1,3});
                c.AddRange(new [] {5,6,8});
                s.Items.Should().BeEquivalentTo(new[]{1,1,3,5,6,8});
            }
        }

        [Fact]
        public void AutomaticallyNestingUsingLinq()
        {
            var a = new CompositeSourceList<CompositeSourceList<int>>();

            var b = new CompositeSourceList<int>();
            var c = new CompositeSourceList<int>();
            var d = new CompositeSourceList<int>();

            var e = 
                from p in a
                from q in p
                select q;

            b.Add(1);
            b.Add(2);
            a.Add(b);

            using (var s = e.Subscribe())
            {
                s.Items.Should().BeEquivalentTo(1,2);
                a.Remove(b);
                s.Items.Should().BeEquivalentTo();
                a.Add(c);
                s.Items.Should().BeEquivalentTo();
                c.Add(2);
                s.Items.Should().BeEquivalentTo(2);
                c.Add(3);
                s.Items.Should().BeEquivalentTo(2,3);
                a.Add(b);
                s.Items.Should().BeEquivalentTo(2,3,1,2);
            }
        }
        [Fact]
        public void AutomaticallyNestingWithTransformationAndLinq()
        {
            var a = new CompositeSourceList<ICompositeList<string>>();

            var b = new CompositeSourceList<int>();
            var c = new CompositeSourceList<int>();

            var e = 
                from p in a
                from q in p
                select q+"x";

            b.Add(1);
            b.Add(2);
            var bt = b.Select(v=>v.ToString());
            a.Add(bt);

            var count = 0;


            using (var s = e.Subscribe())
            {
                s.WhenAnyValue(p => p.Items).Subscribe(_ => count++);
                s.Items.Should().BeEquivalentTo("1x", "2x");
                a.Remove(bt);
                s.Items.Should().BeEquivalentTo();
                a.Add(c.Select(v=>v.ToString()));
                s.Items.Should().BeEquivalentTo();
                c.Add(2);
                s.Items.Should().BeEquivalentTo("2x");
                c.Add(3);
                s.Items.Should().BeEquivalentTo("2x", "3x");
                a.Add(bt);
                s.Items.Should().BeEquivalentTo("2x", "3x", "1x", "2x");
            }

            // 6 changes should be recorded
            count.Should().Be(6);
        }
    }
}
