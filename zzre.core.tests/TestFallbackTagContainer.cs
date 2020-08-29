using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.core.tests
{
    [TestFixture]
    public class TestFallbackTagContainer
    {
        private class Tag1 { }
        private class Tag2 { }
        private class Tag3 { }
        private class Tag4 { }
        private class SubTag1Of1 : Tag1 { }
        private class SubTag2Of1 : Tag1 { }

        private ITagContainer fallback = null!;
        private ITagContainer main = null!;
        private ITagContainer container = null!;

        [SetUp]
        public void Setup()
        {
            fallback = new TagContainer();
            fallback.AddTag<Tag1>(new SubTag1Of1());
            fallback.AddTag(new Tag2());
            main = new TagContainer();
            main.AddTag(new Tag3());
            main.AddTag(new SubTag2Of1() as Tag1);
            container = main.FallbackTo(fallback);
        }

        [Test]
        public void HasAllTags()
        {
            Assert.IsTrue(container.HasTag<Tag1>());
            Assert.IsTrue(container.HasTag<Tag2>());
            Assert.IsTrue(container.HasTag<Tag3>());
        }

        [Test]
        public void HasOverwrittenTagFromParent()
        {
            Assert.IsTrue(container.HasTag<SubTag2Of1>());
            Assert.AreSame(container.GetTag<SubTag2Of1>(), container.GetTag<Tag1>());
        }

        [Test]
        public void GetTagsTakesFromBoth()
        {
            var tags = container.GetTags<Tag1>();
            Assert.AreEqual(2, tags.Count());
            var tagTypes = tags.Select(t => t.GetType());
            Assert.That(tagTypes, Is.EquivalentTo(new Type[] { typeof(SubTag1Of1), typeof(SubTag2Of1) }));
        }

        [Test]
        public void CannotBeModified()
        {
            Assert.That(() => container.AddTag(new object()), Throws.Exception);
            Assert.That(() => container.RemoveTag<object>(), Throws.Exception);
        }
    }
}
