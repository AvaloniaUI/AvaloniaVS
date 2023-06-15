using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

namespace CompletionEngineTests;

/// <summary>
///  Test Scenario
/// </summary>
/// <param name="Description"></param>
/// <param name="Expected"></param>
/// <param name="Agrument"></param>
public record class Scenario(string Description, object Expected, object Agrument)
{
    public override string ToString()
    {
        return Description;
    }
}

/// <summary>
/// Provides a data source for a data theory, with the data coming from inline values.
/// </summary>
public sealed class ScenarioAttribute : DataAttribute
{
    readonly object[] data;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioAttribute"/> class.
    /// </summary>
    /// <param name="description">The description of test scenario</param>
    /// <param name="expected">The expected value</param>
    /// <param name="agrument">The argument of pass to test method.</param>
    public ScenarioAttribute(string description, object expected, object agrument)
    {
        Scenario scenario = new(description, expected, agrument);
        data = new object[] { scenario };
    }

    /// <inheritdoc/>
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        yield return data;
    }
}
