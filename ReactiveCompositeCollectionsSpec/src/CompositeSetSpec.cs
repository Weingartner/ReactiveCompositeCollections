using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using ReactiveUI;
using Weingartner.ReactiveCompositeCollections;
using Xunit;

namespace Weingartner.Utils.Spec
{
    public class CompositeSetSpec
    {
        [Fact]
        public void AnEmptySetIsEmpty()
        {
            var a = new CompositeSourceSet<int> {};
            var b = new CompositeSourceSet<int> {};
            var c = new CompositeSourceSet<int> {};

            var x = new CompositeSet<int>(a,b);
            var y = new CompositeSet<int>(x,c);

            using (var s = y.Subscribe())
            {
                a.Source=a.Source.Add(1);
                s.Items.ShouldBeEquivalentTo(new[]{1});
                b.Source=b.Source.Add(1);
                s.Items.Should().BeEquivalentTo(new[]{1});
                b.Source=b.Source.Add(3);
                s.Items.Should().BeEquivalentTo(new[]{1,3});
                c.Source=c.Source.Union(new [] {5,6,8});
                s.Items.Should().BeEquivalentTo(new[]{1,3,5,6,8});
            }
        }

        [Fact]
        public void NestedDynamicSources()
        {
            var a = new CompositeSourceSet<CompositeSourceSet<int>>();

            var b = new CompositeSourceSet<int>();
            var c = new CompositeSourceSet<int>();
            var d = new CompositeSourceSet<int>();

            var e = 
                from p in a
                from q in p
                select q;

            b.Source=b.Source.Add(1);
            b.Source=b.Source.Add(2);
            a.Source=a.Source.Add(b);

            using (var s = e.Subscribe())
            {
                s.Items.Should().BeEquivalentTo(1,2);
                a.Source=a.Source.Remove(b);
                s.Items.Should().BeEquivalentTo();
                a.Source=a.Source.Add(c);
                s.Items.Should().BeEquivalentTo();
                c.Source=c.Source.Add(2);
                s.Items.Should().BeEquivalentTo(2);
                c.Source=c.Source.Add(3);
                s.Items.Should().BeEquivalentTo(2,3);
                a.Source=a.Source.Add(b);
                s.Items.Should().BeEquivalentTo(2,3,1);
            }
        }
        [Fact]
        public void TransformedDynamicSources()
        {
            var a = new CompositeSourceSet<ICompositeSet<string>>();

            var b = new CompositeSourceSet<int>();
            var c = new CompositeSourceSet<int>();

            var e = 
                from p in a
                from q in p
                select q+"x";

            b.Source=b.Source.Add(1);
            b.Source=b.Source.Add(2);
            var bt = b.Select(v=>v.ToString());
            a.Source=a.Source.Add(bt);

            var count = 0;

            using (var s = e.Subscribe())
            {
                s.WhenAnyValue(p => p.Items).Subscribe(_ => count++);
                s.Items.Should().BeEquivalentTo("1x", "2x");
                a.Source=a.Source.Remove(bt);
                s.Items.Should().BeEquivalentTo();
                a.Source=a.Source.Add(c.Select(v=>v.ToString()));
                s.Items.Should().BeEquivalentTo();
                c.Source=c.Source.Add(2);
                s.Items.Should().BeEquivalentTo("2x");
                c.Source=c.Source.Add(3);
                s.Items.Should().BeEquivalentTo("2x", "3x");
                a.Source=a.Source.Add(bt);
                s.Items.Should().BeEquivalentTo("2x", "3x", "1x");
            }
            // 6 changes should be recorded
            count.Should().Be(5);
        }
    }
}
