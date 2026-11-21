using NUnit.Framework;
using Ski.Core;

namespace Ski.Tests
{
    public class AccountTests
    {
        [Test]
        public void NamesFollowTheServiceRules()
        {
            Assert.IsNull(AccountRules.CheckName("erik_99"));
            Assert.IsNull(AccountRules.CheckName("a.b-c@d"));
            Assert.AreEqual("Name: 3-20 characters", AccountRules.CheckName("ab"));
            Assert.AreEqual("Name: 3-20 characters", AccountRules.CheckName(new string('x', 21)));
            Assert.AreEqual("Name: no spaces", AccountRules.CheckName("big jonte"));
            Assert.AreEqual("Name: letters, digits", AccountRules.CheckName("åsa"));
        }

        [Test]
        public void PasswordsSayWhatTheyStillNeed()
        {
            Assert.IsNull(AccountRules.CheckPassword("Powder1!"));
            Assert.AreEqual("Password: 8-30 long", AccountRules.CheckPassword("Pw1!"));
            Assert.AreEqual("Password needs A-Z", AccountRules.CheckPassword("powder12!"));
            Assert.AreEqual("Password needs a-z", AccountRules.CheckPassword("POWDER12!"));
            Assert.AreEqual("Password needs 0-9", AccountRules.CheckPassword("Powderly!"));
            Assert.AreEqual("Password needs a symbol", AccountRules.CheckPassword("Powder123"));
        }
    }
}
