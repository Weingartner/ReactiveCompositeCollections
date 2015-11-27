using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

            var x = a.Concat(b);
            var y = x.Concat(c);

            using (var s = y.Subscribe())
            {
                s.Items.Should().BeEmpty();
                a.Source=a.Source.Add(1);
                s.Items.ShouldBeEquivalentTo(new[]{1});
                b.Source=b.Source.Add(1);
                s.Items.Should().BeEquivalentTo(new[]{1,1});
                b.Source=b.Source.Add(3);
                s.Items.Should().BeEquivalentTo(new[]{1,1,3});
                c.Source=c.Source.AddRange(new [] {5,6,8});
                s.Items.Should().BeEquivalentTo(new[]{1,1,3,5,6,8});
            }
        }

        [Fact]
        public void SelectManyWorkWithIEnumerable()
        {
            var a = new CompositeSourceList<IList<int>>();

            var b = a.SelectMany(v => v);

            using (var r = b.Subscribe())
            {
                r.Items.Count.Should().Be(0);
                a.Source = a.Source.Add(new List<int> {1, 2, 3});
                r.Items.Should().BeEquivalentTo(1, 2, 3);
            }

        }

        [Fact]
        public void SelectManyLinqWorkWithIEnumerable()
        {
            var a = new CompositeSourceList<IList<int>>();

            var b = from v in a
                    from q in v
                    select q;

            using (var r = b.Subscribe())
            {
                r.Items.Count.Should().Be(0);
                a.Source = a.Source.Add(new List<int> {1, 2, 3});
                r.Items.Should().BeEquivalentTo(1, 2, 3);
            }

        }

        [Fact]
        public void ShouldBeAbleToConcatLists()
        {

            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();
            var c = a.Concat(b);

            using (var r = c.Subscribe())
            {
                r.Items.Count.Should().Be(0);
                a.AddRange(new List<int> {1,2,3});
                r.Items.Should().BeEquivalentTo(1, 2, 3);
                b.AddRange(new List<int> {5,6,7});
                r.Items.Should().BeEquivalentTo(1, 2, 3,5,6,7);
                a.Source = ImmutableList<int>.Empty;
                r.Items.Should().BeEquivalentTo(5,6,7);
            }

        }

        [Fact]
        public void WhereShouldWork()
        {
            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();
            var c = a.Concat(b).Where(v=>v>5);

            using (var r = c.Subscribe())
            {
                r.Items.Count.Should().Be(0);
                a.AddRange(new List<int> {1,2,3});
                r.Items.Should().BeEquivalentTo();
                b.AddRange(new List<int> {5,6,7});
                r.Items.Should().BeEquivalentTo(6,7);
                a.Source = ImmutableList<int>.Empty;
                r.Items.Should().BeEquivalentTo(6,7);
            }
            
        }

        [Fact]
        public void WhereManyShouldWork()
        {
            var a = new CompositeSourceList<CompositeSourceList<int>>();

            // Note that clist.Any(v => v>10) returns IObservable<bool>

            var b = from clist in a
                    where clist.Any(v => v > 10)
                    select clist;

            using (var s = b.Subscribe())
            {
                var x = new CompositeSourceList<int>();
                var y = new CompositeSourceList<int>();
                a.Add(x);
                a.Add(y);
                s.Items.Count.Should().Be(0);
                x.Add(9);
                s.Items.Count.Should().Be(0);
                x.Add(11);
                s.Items.Count.Should().Be(1);
                y.Add(12);
                s.Items.Count.Should().Be(2);
                x.Remove(11);
                s.Items.Count.Should().Be(1);
                x.Remove(9);
                s.Items.Count.Should().Be(1);
                y.Remove(12);
                s.Items.Count.Should().Be(0);
            }

        }

        [Fact]
        public void AddedObservableShouldWork()
        {
            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();
            var c = a.Concat(b);

            var addedResult = new List<IReadOnlyCollection<int>>();
            var removedResult = new List<IReadOnlyCollection<int>>();

            c.AddedToSetObservable().Subscribe(added => addedResult.Add(added));
            c.RemovedFromSetObservable().Subscribe(removed => removedResult.Add(removed));

            a.AddRange(new [] {1,2});
            b.AddRange(new [] {8,2});

            b.Remove(8);
            a.Remove(2); // Doesn't cause a remove event as 2 belongs to both sources
            a.Remove(1);

            addedResult[0].Should().BeEquivalentTo(1, 2);
            addedResult[1].Should().BeEquivalentTo(8);

            removedResult[0].Should().BeEquivalentTo(8);
            removedResult[1].Should().BeEquivalentTo(1);




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

            b.Source = b.Source.Add(1);
            b.Source = b.Source.Add(2);
            a.Source = a.Source.Add(b);

            using (var s = e.Subscribe())
            {
                s.Items.Should().BeEquivalentTo(1,2);
                a.Source = a.Source.Remove(b);
                s.Items.Should().BeEquivalentTo();
                a.Source = a.Source.Add(c);
                s.Items.Should().BeEquivalentTo();
                c.Source = c.Source.Add(2);
                s.Items.Should().BeEquivalentTo(2);
                c.Source = c.Source.Add(3);
                s.Items.Should().BeEquivalentTo(2,3);
                a.Source=a.Source.Add(b);
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
                s.Items.Should().BeEquivalentTo("2x", "3x", "1x", "2x");
            }

            // 6 changes should be recorded
            count.Should().Be(5);
        }
    }
}
