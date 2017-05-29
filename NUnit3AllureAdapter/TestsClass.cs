using NUnit.Framework;


namespace NUnit3AllureAdapterTests
{
    [TestFixture]
    public class TestsClass
    {
        [Test]
        public void SuccessTest()
        {
            Assert.True(true);
        }
        [Test]
        public void FailureTest()
        {
            Assert.Fail();
        }
    }
}
