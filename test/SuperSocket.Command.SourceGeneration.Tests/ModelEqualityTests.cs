using System;
using System.Reflection;
using SuperSocket.Command.SourceGeneration.Model;
using Xunit;

namespace SuperSocket.Command.SourceGeneration.Tests;

public class ModelEqualityTests
{
    [Fact]
    public void RegistryModels_WithEquivalentContainingTypes_AreEqual()
    {
        var first = new RegistryModel(
            "Demo",
            "Reg",
            "public",
            "string",
            "global::Demo.FakePkg",
            new EquatableArray<string>(["global::Demo.IFakePkg"]),
            true,
            false,
            new EquatableArray<ContainingTypeModel>([
                new ContainingTypeModel("public", string.Empty, "class", "Outer", string.Empty, new EquatableArray<string>(["where T : class"])),
            ]));

        var second = new RegistryModel(
            "Demo",
            "Reg",
            "public",
            "string",
            "global::Demo.FakePkg",
            new EquatableArray<string>(["global::Demo.IFakePkg"]),
            true,
            false,
            new EquatableArray<ContainingTypeModel>([
                new ContainingTypeModel("public", string.Empty, "class", "Outer", string.Empty, new EquatableArray<string>(["where T : class"])),
            ]));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ContainingTypeModels_WithEquivalentConstraintClauses_AreEqual()
    {
        var first = new ContainingTypeModel("public", string.Empty, "class", "Outer", "<T>", new EquatableArray<string>(["where T : class"]));
        var second = new ContainingTypeModel("public", string.Empty, "class", "Outer", "<T>", new EquatableArray<string>(["where T : class"]));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EquatableArray_ArrayConstructor_ReusesProvidedArray()
    {
        string[] items = ["one", "two"];

        var array = new EquatableArray<string>(items);

        FieldInfo field = typeof(EquatableArray<string>).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Same(items, field.GetValue(array));
    }
}
