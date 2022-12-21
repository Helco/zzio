using NUnit.Framework;
using System.Linq;

namespace zzre.core.tests;

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
        Assert.Less(indexByItem[item1], indexByItem[item2]);
        Assert.Less(indexByItem[item2], indexByItem[item3]);
        Assert.Less(indexByItem[item3], indexByItem[item4]);
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
        Assert.Less(indexByItem[item1], indexByItem[item2]);
        Assert.Less(indexByItem[item2], indexByItem[item3]);
        Assert.Less(indexByItem[item3], indexByItem[item4]);
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
        Assert.Less(indexByItem[item1], indexByItem[item2]);
        Assert.Less(indexByItem[item2], indexByItem[item3]);
        Assert.Less(indexByItem[item3], indexByItem[item4]);
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
        Assert.Less(indexByItem[item1], indexByItem[item2]);
        Assert.Less(indexByItem[item2], indexByItem[item3]);
        Assert.Less(indexByItem[item3], indexByItem[item4]);
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
        Assert.Less(indexByItem[item1], indexByItem[item2]);
        Assert.Less(indexByItem[item2], indexByItem[item3]);
    }
}