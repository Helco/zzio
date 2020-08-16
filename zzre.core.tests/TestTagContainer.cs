using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.core.tests
{
    [TestFixture]
    public class TestTagContainer
    {
        private class Tag1 { }
        private class SubTag1Of1 : Tag1 { }
        private class SubTag2Of1 : Tag1 { }

        private ITagContainer container = null!;

        [SetUp]
        public void Setup()
        {
            container = new TagContainer();
        }

        [Test]
        public void StartsEmpty()
        {
            Assert.IsEmpty(container.GetTags<object>());
        }

        [Test]
        public void GetTagThrows()
        {
            Assert.That(() => container.GetTag<Tag1>(), Throws.InstanceOf<Exception>());
        }

        [Test]
        public void CanAddAndRemoveNewTag()
        {
            Assert.IsFalse(container.HasTag<Tag1>());
            Assert.IsFalse(container.RemoveTag<Tag1>());
            container.AddTag(new Tag1());
            Assert.IsTrue(container.HasTag<Tag1>());
            Assert.IsTrue(container.RemoveTag<Tag1>());
            Assert.IsFalse(container.HasTag<Tag1>());
            Assert.IsFalse(container.RemoveTag<Tag1>());
        }

        [Test]
        public void CanFindSubTag()
        {
            container.AddTag<Tag1>(new SubTag1Of1());
            Assert.IsTrue(container.HasTag<SubTag1Of1>());
        }

        [Test]
        public void CanAddAndRemoveTwoSubTags()
        {
            var subTag1 = new SubTag1Of1();
            var subTag2 = new SubTag2Of1();
            container.AddTag(subTag1);
            container.AddTag(subTag2);
            Assert.IsTrue(container.HasTag<Tag1>());
            Assert.IsTrue(container.HasTag<SubTag1Of1>());
            Assert.IsTrue(container.HasTag<SubTag2Of1>());

            var gotForTag1 = container.GetTag<Tag1>();
            Assert.IsTrue(gotForTag1 == subTag1 || gotForTag1 == subTag2);
            var gotAllForTag1 = container.GetTags<Tag1>();
            Assert.That(gotAllForTag1, Is.EquivalentTo(new Tag1[] { subTag1, subTag2 }));

            container.RemoveTag<SubTag1Of1>();
            Assert.IsTrue(container.HasTag<Tag1>());
            Assert.IsFalse(container.HasTag<SubTag1Of1>());
            Assert.IsTrue(container.HasTag<SubTag2Of1>());

            container.RemoveTag<Tag1>();
            Assert.IsFalse(container.HasTag<Tag1>());
            Assert.IsFalse(container.HasTag<SubTag2Of1>());
        }
    }
}
