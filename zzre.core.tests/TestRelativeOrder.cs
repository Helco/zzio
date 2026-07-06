using NUnit.Framework;
using System;
using System.Linq;

namespace zzre.tests;

public class TestRelativeOrder
{
    private static T Identity<T>(T t) => t;

    [Test]
    public void unconstrained()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);
        solver.SolveFor(new[]
        {
            new RelativeOrderItem(),
            new RelativeOrderItem(),
            new RelativeOrderItem(),
            new RelativeOrderItem()
        });
    }

    [Test]
    public void afterForward()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item1 = new RelativeOrderItem();
        var item2 = new RelativeOrderItem().After(item1);
        var item3 = new RelativeOrderItem().After(item2);
        var item4 = new RelativeOrderItem().After(item3).After(item2).After(item1);
        solver.SolveFor(new[] { item1, item2, item3, item4 });

        var indexByItem = solver
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item, p => p.index);
        Assert.That(indexByItem[item1], Is.LessThan(indexByItem[item2]));
        Assert.That(indexByItem[item2], Is.LessThan(indexByItem[item3]));
        Assert.That(indexByItem[item3], Is.LessThan(indexByItem[item4]));
    }

    [Test]
    public void afterForwardIndex()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item1 = new RelativeOrderItem();
        var item2 = new RelativeOrderItem().After(item1);
        var item3 = new RelativeOrderItem().After(item2);
        var item4 = new RelativeOrderItem().After(item3).After(item2).After(item1);
        solver.SolveFor(new[] { item1, item2, item3, item4 });

        Assert.That(solver.Count, Is.EqualTo(4));
        Assert.That(solver[0], Is.SameAs(item1));
        Assert.That(solver[1], Is.SameAs(item2));
        Assert.That(solver[2], Is.SameAs(item3));
        Assert.That(solver[3], Is.SameAs(item4));
        Assert.That(() => solver[4], Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => solver[-1], Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void afterBackward()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item1 = new RelativeOrderItem();
        var item2 = new RelativeOrderItem().After(item1);
        var item3 = new RelativeOrderItem().After(item2);
        var item4 = new RelativeOrderItem().After(item3).After(item2).After(item1);
        solver.SolveFor(new[] { item4, item3, item2, item1 });

        var indexByItem = solver
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item, p => p.index);
        Assert.That(indexByItem[item1], Is.LessThan(indexByItem[item2]));
        Assert.That(indexByItem[item2], Is.LessThan(indexByItem[item3]));
        Assert.That(indexByItem[item3], Is.LessThan(indexByItem[item4]));
    }

    [Test]
    public void beforeForward()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item4 = new RelativeOrderItem();
        var item3 = new RelativeOrderItem().Before(item4);
        var item2 = new RelativeOrderItem().Before(item3);
        var item1 = new RelativeOrderItem().Before(item4).Before(item3).Before(item2);
        solver.SolveFor(new[] { item1, item2, item3, item4 });

        var indexByItem = solver
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item, p => p.index);
        Assert.That(indexByItem[item1], Is.LessThan(indexByItem[item2]));
        Assert.That(indexByItem[item2], Is.LessThan(indexByItem[item3]));
        Assert.That(indexByItem[item3], Is.LessThan(indexByItem[item4]));
    }

    [Test]
    public void beforeBackward()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item4 = new RelativeOrderItem();
        var item3 = new RelativeOrderItem().Before(item4);
        var item2 = new RelativeOrderItem().Before(item3);
        var item1 = new RelativeOrderItem().Before(item4).Before(item3).Before(item2);
        solver.SolveFor(new[] { item4, item3, item2, item1 });

        var indexByItem = solver
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item, p => p.index);
        Assert.That(indexByItem[item1], Is.LessThan(indexByItem[item2]));
        Assert.That(indexByItem[item2], Is.LessThan(indexByItem[item3]));
        Assert.That(indexByItem[item3], Is.LessThan(indexByItem[item4]));
    }

    [Test]
    public void inbetween()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item1 = new RelativeOrderItem();
        var item3 = new RelativeOrderItem();
        var item2 = new RelativeOrderItem().Before(item3).After(item1);
        solver.SolveFor(new[] { item1, item3, item2 });

        var indexByItem = solver
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item, p => p.index);
        Assert.That(indexByItem[item1], Is.LessThan(indexByItem[item2]));
        Assert.That(indexByItem[item2], Is.LessThan(indexByItem[item3]));
    }

    [Test]
    public void unsolvable()
    {
        var solver = new RelativeOrderSolver<RelativeOrderItem>(Identity);

        var item1 = new RelativeOrderItem();
        var item2 = new RelativeOrderItem().Before(item1);
        item1 = item1.Before(item2);

        Assert.That(solver.TrySolveFor([item1, item2]), Is.False);
        Assert.That(() => solver.SolveFor([item1, item2]), Throws.ArgumentException);
    }
}