﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using FluentAssertions;
using ReactiveUI;
using Weingartner.ReactiveCompositeCollections;
using Xunit;

namespace Weingartner.ReactiveCompositeCollectionsSpec
{
    public class CompositeSourceListSpec
    {
        [Fact]
        public void TakeShouldWork()
        {
            var a = new CompositeSourceList<int>();
            var b = a.Take( 5 );

            using (var s = b.Subscribe())
            {
                s.Items.Should().BeEmpty();
                a.AddRange( Enumerable.Range( 0,10 ) );
                s.Items.Should().BeEquivalentTo( Enumerable.Range( 0,5 ) );
                a.InsertAt( 0,15 );
                s.Items.Should().BeEquivalentTo( new[] {15, 0, 1, 2, 3} );
                a.Remove( 3 );
                s.Items.Should().BeEquivalentTo( new[] {15, 0, 1, 2, 4} );
            }

        }

        [Fact]
        public void DynamicTakeShouldWork()
        {
            var aa = new CompositeSourceList<int>();
            var filter = new BehaviorSubject<int>( 0 );

            var c =
                from f in filter
                from a in aa.Take( f )
                select a;

            using (var s = c.Subscribe())
            {
                s.Items.Should().BeEmpty();
                aa.AddRange( Enumerable.Range( 0,10 ) );
                s.Items.Should().BeEmpty();
                filter.OnNext( 3 );
                s.Items.Should().BeEquivalentTo( new []{0,1,2} );
                aa.InsertAt( 0,15 );
                s.Items.Should().BeEquivalentTo( new []{15,0,1} );
                filter.OnNext( 2 );
                s.Items.Should().BeEquivalentTo( new []{15,0} );
                filter.OnNext( 4 );
                s.Items.Should().BeEquivalentTo( new []{15,0,1,2} );

            }

        }


        [Fact]
        public void TakeWithSelectManyShouldWork()
        {
            var aa = new CompositeSourceList<int>(  );
            var bb = new CompositeSourceList<int>(  );


            var cc = (from a in aa
                     from b in bb
                     select (a, b)).Take( 4 );


            using (var s = cc.Subscribe())
            {
                aa.AddRange( new []{0,1} );
                bb.AddRange( new []{10,11} );

                s.Items.Should().BeEquivalentTo( new[]{(0,10), (0,11), (1,10), (1,11)} );

                bb.InsertAt( 0, 99 );

                s.Items.Should().BeEquivalentTo( new[]{(0,99), (0,10), (0,11), (1,99)} );
            }
        }

        [Fact]
        public void TakeWithSelectManyAndLetShouldWork()
        {
            var aa = new CompositeSourceList<int>(  );
            var bb = new CompositeSourceList<int>(  );


            var cc = (from a in aa
                     from b in bb
                     let sum = a + b
                     select sum
                     ).Take( 4 );


            using (var s = cc.Subscribe())
            {
                aa.AddRange( new []{0,1} );
                bb.AddRange( new []{10,11} );

                s.Items.Should().BeEquivalentTo( new[]{10, 11, 11, 12} );

                bb.InsertAt( 0, 99 );

                s.Items.Should().BeEquivalentTo( new[]{99, 10, 11, 100} );
            }



        }
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
                s.Items.Should().BeEquivalentTo(new[]{1});
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
                r.Items.Should().BeEquivalentTo(new[] {1, 2, 3});
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
                r.Items.Should().BeEquivalentTo(new[] {1, 2, 3});
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
                r.Items.Should().BeEquivalentTo(new[] {1, 2, 3});
                b.AddRange(new List<int> {5,6,7});
                r.Items.Should().BeEquivalentTo(new[] {1, 2, 3,5,6,7});
                a.Source = ImmutableList<int>.Empty;
                r.Items.Should().BeEquivalentTo(new[] {5,6,7});
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
                r.Items.Should().BeEmpty();
                b.AddRange(new List<int> {5,6,7});
                r.Items.Should().BeEquivalentTo(new[]{6,7});
                a.Source = ImmutableList<int>.Empty;
                r.Items.Should().BeEquivalentTo(new[]{6,7});
            }
            
        }

        [Fact]
        public void DynamicWhereShouldWork()
        {
            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();

            // A filter that is updated at runtime
            // and combined into the query
            var filter = new BehaviorSubject<Func<int,bool>>(v=>v>5);

            var c = from f in filter
                    from item in a.Concat( b )
                    where f( item )
                    select item;

            using (var r = c.Subscribe())
            {
                r.Items.Count.Should().Be(0);
                a.AddRange(new List<int> {1,2,3});
                r.Items.Should().BeEmpty();
                b.AddRange(new List<int> {5,6,7});
                r.Items.Should().BeEquivalentTo(new[]{6,7});

                // Change the filter to allow all items.
                filter.OnNext(v=>true);
                r.Items.Should().BeEquivalentTo(new[]{1, 2, 3, 5, 6, 7});
            }            
        }


        [Fact]
        public void WhereShouldBePerformant()
        {
            var a = new CompositeSourceList<CompositeSourceList<CompositeSourceList<int>>>();

            // Note that clist.Any(v => v>10) returns IObservable<bool>

            var b = from clist in a
                    from t in clist
                    where t.Any(v => v > 10)
                    select t.Select(v=>v+1);

            using (var s = b.Subscribe())
            {
                var x = new CompositeSourceList<CompositeSourceList<int>>();
                var y = new CompositeSourceList<CompositeSourceList<int>>();
                a.Add(x);
                a.Add(y);
                var xx = new CompositeSourceList<int>();
                var yy = new CompositeSourceList<int>();
                x.Add(xx);
                y.Add(yy);
                for (int i = 0; i < 100000; i++)
                {
                    xx.Add(i);
                    yy.Add(i+1);
                }
                s.Items.Count.Should().BeGreaterThan(0);
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

            addedResult[0].Should().BeEquivalentTo(new[]{1, 2});
            addedResult[1].Should().BeEquivalentTo(new[]{8});

            removedResult[0].Should().BeEquivalentTo(new[]{8});
            removedResult[1].Should().BeEquivalentTo(new[]{1});




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
                s.Items.Should().BeEquivalentTo(new[] {1,2});
                a.Source = a.Source.Remove(b);
                s.Items.Should().BeEmpty();
                a.Source = a.Source.Add(c);
                s.Items.Should().BeEmpty();
                c.Source = c.Source.Add(2);
                s.Items.Should().BeEquivalentTo(new[] {2});
                c.Source = c.Source.Add(3);
                s.Items.Should().BeEquivalentTo(new[] {2,3});
                a.Source=a.Source.Add(b);
                s.Items.Should().BeEquivalentTo(new[] {2,3,1,2});
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
                s.PropertyChanged += (_,__)=>count++;
                s.Items.Should().BeEquivalentTo(new[] {"1x", "2x"});
                a.Source=a.Source.Remove(bt);
                s.Items.Should().BeEmpty();
                a.Source=a.Source.Add(c.Select(v=>v.ToString()));
                s.Items.Should().BeEmpty();
                c.Source=c.Source.Add(2);
                s.Items.Should().BeEquivalentTo(new[] {"2x"});
                c.Source=c.Source.Add(3);
                s.Items.Should().BeEquivalentTo(new[] {"2x", "3x"});
                a.Source=a.Source.Add(bt);
                s.Items.Should().BeEquivalentTo(new[] {"2x", "3x", "1x", "2x"});
            }

            // 6 changes should be recorded
            count.Should().Be(4);
        }
        [Fact]
        public void BindingShouldWork()
        {
            var a = new CompositeSourceList<int>();
            var b = new CompositeSourceList<int>();
            var c = new CompositeSourceList<int>();

            var d = b.Concat(c);

            d.Bind(a);
        }

    }
}
