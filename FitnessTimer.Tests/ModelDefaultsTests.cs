using FitnessTimer.App.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FitnessTimer.Tests;

[TestClass]
public sealed class ModelDefaultsTests
{
    [TestMethod]
    public void TrainingPlanCreateDefaultReturnsExpectedGroups()
    {
        TrainingPlan plan = TrainingPlan.CreateDefault();

        Assert.AreNotEqual(Guid.Empty, plan.Id);
        Assert.AreEqual("默认方案", plan.Name);
        Assert.AreEqual(3, plan.Groups.Count);

        TrainingGroup firstGroup = plan.Groups[0];
        Assert.AreEqual(1, firstGroup.Order);
        Assert.AreEqual("动作 1", firstGroup.ActionName);
        Assert.AreEqual(30, firstGroup.TrainingSeconds);
        Assert.AreEqual(15, firstGroup.RestSeconds);

        Assert.AreEqual(2, plan.Groups[1].Order);
        Assert.AreEqual("动作 2", plan.Groups[1].ActionName);
        Assert.AreEqual(3, plan.Groups[2].Order);
        Assert.AreEqual("动作 3", plan.Groups[2].ActionName);
    }

    [TestMethod]
    public void TrainingPlanLibraryCreateDefaultReturnsActiveDefaultPlan()
    {
        TrainingPlanLibrary library = TrainingPlanLibrary.CreateDefault();

        Assert.AreEqual(1, library.Plans.Count);
        Assert.AreEqual(library.Plans[0].Id, library.ActivePlanId);
        Assert.AreEqual("默认方案", library.Plans[0].Name);
    }

    [TestMethod]
    public void AppSettingsCreateDefaultReturnsExpectedValues()
    {
        AppSettings settings = AppSettings.CreateDefault();

        Assert.AreEqual(5, settings.PreparationSeconds);
        Assert.AreEqual("Timer", settings.LastPage);
        Assert.IsFalse(settings.CompactAlwaysOnTop);
        Assert.IsFalse(settings.UseDarkMode);
        Assert.IsNull(settings.CompactLeft);
        Assert.IsNull(settings.CompactTop);
        Assert.IsNull(settings.CompactWidth);
        Assert.IsNull(settings.CompactHeight);
        Assert.IsFalse(settings.IsCompactMode);
    }
}
