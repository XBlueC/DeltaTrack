using System.Collections.Immutable;
using DeltaTrack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Generator.Tests;

public class TrackableGeneratorTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
        .Append(typeof(ITrackable).Assembly.Location).Distinct()
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    [Theory]
    [InlineData("[DeltaTrack.TrackableField] private int _value;")]
    [InlineData("[DeltaTrack.TrackableFieldAttribute] private int _value;")]
    [InlineData("[global::DeltaTrack.TrackableFieldAttribute] private int _value;")]
    [InlineData("[Alias] private int _value;")]
    [InlineData("[Alias] private int _first, _second;")]
    public void FieldAttributeSpellingsGenerateOnce(string fields)
    {
        var result = Generate("using Alias = DeltaTrack.TrackableFieldAttribute; public partial class Model { " +
                              fields + " }");
        Assert.Single(result.GeneratedSources);
        Assert.Contains("public int ", result.GeneratedSources[0].SourceText.ToString());
    }

    [Fact]
    public void PartialDeclarationsProduceOneCompleteType()
    {
        var result = Generate(
            "using DeltaTrack; [Trackable] public partial class Model { private int _first; }",
            "using DeltaTrack; public partial class Model { [TrackableField] private string _second; }");
        var source = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("public int First", source);
        Assert.Contains("public string Second", source);
    }

    [Fact]
    public void FieldOnlyPartialDeclarationsGenerateOnce()
    {
        var result = Generate(
            "using DeltaTrack; public partial class Model { [TrackableField] private int _first; }",
            "using DeltaTrack; public partial class Model { [TrackableField] private int _second; }");
        var source = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("public int First", source);
        Assert.Contains("public int Second", source);
    }

    [Theory]
    [InlineData("[Trackable] public partial class Model<T> where T : class { private T _value; }")]
    [InlineData(
        "public partial class Outer<T> where T : class { [Trackable] public partial class Model<U> where U : struct { private T _value; private U _count; } }")]
    [InlineData("public static partial class Outer { [Trackable] public partial class Model { private int _value; } }")]
    [InlineData("public partial struct Outer { [Trackable] public partial class Model { private int _value; } }")]
    public void GenericAndNestedTypesCompile(string declaration)
    {
        Assert.Single(Generate("using DeltaTrack; " + declaration).GeneratedSources);
    }

    [Fact]
    public void HintNamesDistinguishContainersAndGenericArity()
    {
        var result = Generate("""
                              using DeltaTrack;
                              [Trackable] public partial class Model { private int _value; }
                              [Trackable] public partial class Model<T> { private T _value; }
                              public partial class Outer { [Trackable] public partial class Model { private int _value; } }
                              """);
        Assert.Equal(3, result.GeneratedSources.Length);
        Assert.Equal(3, result.GeneratedSources.Select(s => s.HintName).Distinct().Count());
    }

    [Fact]
    public void ImmutableAndStaticFieldsDoNotGenerateSetters()
    {
        var result = Generate("""
                              using DeltaTrack;
                              [Trackable] public partial class Model {
                                  private readonly int _readOnly;
                                  private static int _shared;
                                  private const int Constant = 1;
                                  int _mutable;
                              }
                              """);
        var source = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("public int Mutable", source);
        Assert.DoesNotContain("public int ReadOnly", source);
        Assert.DoesNotContain("public int Shared", source);
    }

    [Theory]
    [InlineData("IList<int>")]
    [InlineData("IDictionary<string, int>")]
    [InlineData("ISet<int>")]
    [InlineData("IntList")]
    public void CollectionsUseTheirImplementedGenericContract(string type)
    {
        var result = Generate(
            "using DeltaTrack; using System.Collections.Generic; public class IntList : List<int> {} " +
            "[Trackable] public partial class Model { private " + type + " _items; }");
        Assert.Contains("global::DeltaTrack.Trackable", Assert.Single(result.GeneratedSources).SourceText.ToString());
    }

    [Fact]
    public void UnrelatedEditsReuseGeneratedSourcesIncludingAttributes()
    {
        var compilation = CreateCompilation("""
                                            using DeltaTrack;
                                            [Trackable] public partial class Model {
                                                [AttachAttribute(typeof(System.ComponentModel.DescriptionAttribute), "value")]
                                                private int _value;
                                            }
                                            public partial class FieldOnly { [TrackableField] private int _count; }
                                            """, "public class Other { public int M() => 1; }");
        var driver = CreateDriver().RunGenerators(compilation);
        var before = driver.GetRunResult().Results.Single().GeneratedSources;
        var other = compilation.SyntaxTrees.Last();
        compilation = compilation.ReplaceSyntaxTree(other,
            CSharpSyntaxTree.ParseText("public class Other { public int M() => 2; }", path: other.FilePath));

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results.Single();
        Assert.Equal(2, result.GeneratedSources.Length);
        foreach (var source in before)
            Assert.Same(source.SourceText,
                result.GeneratedSources.Single(s => s.HintName == source.HintName).SourceText);
        Assert.All(result.TrackedOutputSteps.Values.SelectMany(s => s).SelectMany(s => s.Outputs),
            output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));
    }

    [Fact]
    public void EditingAnotherPartialDeclarationUpdatesTheOutput()
    {
        var compilation = CreateCompilation(
            "using DeltaTrack; [Trackable] public partial class Model { private int _first; }",
            "public partial class Model { private int _second; }");
        var driver = CreateDriver().RunGenerators(compilation);
        var old = compilation.SyntaxTrees.Last();
        compilation = compilation.ReplaceSyntaxTree(old,
            CSharpSyntaxTree.ParseText("public partial class Model { private string _second; }", path: old.FilePath));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        AssertNoErrors(output);
        Assert.Contains("public string Second",
            driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString());
    }

    [Fact]
    public void IncompleteTypeDoesNotCrashOrRemoveOtherModels()
    {
        var compilation = CreateCompilation("""
                                            using DeltaTrack;
                                            [Trackable] public partial class Broken { private MissingType _value; }
                                            [Trackable] public partial class Good { private int _value; }
                                            """);
        var result = CreateDriver().RunGenerators(compilation).GetRunResult().Results.Single();
        Assert.Null(result.Exception);
        Assert.Equal("Good.Track.g.cs", Assert.Single(result.GeneratedSources).HintName);
    }

    [Theory]
    [InlineData("[Trackable] public partial class Base { private int _value; }",
        "[Trackable] public partial class Derived : Base { private int _other; }")]
    [InlineData("public partial class Base { [TrackableField] private int _value; }",
        "[Trackable] public partial class Derived : Base { private int _other; }")]
    [InlineData("[Trackable] public partial class Base { private int _value; }",
        "public partial class Derived : Base { [TrackableField] private int _other; }")]
    [InlineData("[Trackable] public partial class Base { private int _value; } public class Middle : Base {}",
        "[Trackable] public partial class Derived : Middle { private int _other; }")]
    public async Task TrackingInheritanceIsRejectedWithoutGeneratingAnotherTracker(string parent, string child)
    {
        var compilation = CreateCompilation("using DeltaTrack; " + parent + child);
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);
        var diagnostics = await output.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TrackableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "TRACK005"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Derived", diagnostic.GetMessage());
        Assert.DoesNotContain(output.GetDiagnostics(), d => d.Id == "CS0108");
    }

    [Fact]
    public async Task UnannotatedDerivedTypeCanUseInheritedTracking()
    {
        var compilation = CreateCompilation("""
                                            using DeltaTrack;
                                            [Trackable] public partial class Base { private int _value; }
                                            public class Derived : Base {}
                                            """);
        var driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);
        AssertNoErrors(output);
        var diagnostics = await output.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TrackableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task UnrelatedAttributeWithTheSameShortNameDoesNotProduceTrackingDiagnostics()
    {
        var compilation = CreateCompilation("""
                                            namespace Other {
                                                public sealed class TrackableAttribute : System.Attribute {}
                                                [Trackable] public class Ordinary {}
                                            }
                                            """);
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TrackableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void CustomSetHasACompilableExplicitlyUnsupportedCopyPath()
    {
        var result = Generate("""
                              using DeltaTrack;
                              using System.Collections.Generic;
                              public class CustomSet : HashSet<int> {}
                              [Trackable] public partial class Model { private CustomSet _items = new(); }
                              """);
        Assert.Contains("NotSupportedException", Assert.Single(result.GeneratedSources).SourceText.ToString());
    }

    private static GeneratorRunResult Generate(params string[] sources)
    {
        var driver = CreateDriver()
            .RunGeneratorsAndUpdateCompilation(CreateCompilation(sources), out var output, out _);
        var result = driver.GetRunResult().Results.Single();
        Assert.Null(result.Exception);
        AssertNoErrors(output);
        return result;
    }

    private static CSharpCompilation CreateCompilation(params string[] sources) => CSharpCompilation.Create(
        "GeneratorProbe",
        sources.Select((source, i) => CSharpSyntaxTree.ParseText(source, path: $"Part{i}.cs")), References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static GeneratorDriver CreateDriver() => CSharpGeneratorDriver.Create(
        new[] { new TrackableGenerator().AsSourceGenerator() },
        driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true));

    private static void AssertNoErrors(Compilation compilation) =>
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
}