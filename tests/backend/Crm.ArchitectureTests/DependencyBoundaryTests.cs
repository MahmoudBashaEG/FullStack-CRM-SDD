using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using Crm.Api.Controllers;
using Crm.Application.Common;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.ArchitectureTests;

public sealed class DependencyBoundaryTests
{
    public static TheoryData<Assembly, string[]> Boundaries => new()
    {
        { typeof(Ticket).Assembly, ["Crm.Application", "Crm.Infrastructure", "Crm.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http"] },
        { typeof(Actor).Assembly, ["Crm.Infrastructure", "Crm.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http"] },
        { typeof(CrmDbContext).Assembly, ["Crm.Api"] }
    };

    [Theory, MemberData(nameof(Boundaries))]
    public void Compiled_dependencies_point_inward(Assembly assembly, string[] forbidden)
    {
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => forbidden.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void Direct_project_dependencies_are_the_approved_graph()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["Crm.Domain"] = [], ["Crm.Application"] = ["Crm.Domain"],
            ["Crm.Infrastructure"] = ["Crm.Application", "Crm.Domain"], ["Crm.Api"] = ["Crm.Application", "Crm.Infrastructure"]
        };
        foreach (var (project, references) in expected)
        {
            var document = XDocument.Load(Path.Combine(Root(), "src", "backend", project, project + ".csproj"));
            var actual = document.Descendants("ProjectReference").Select(node => Path.GetFileNameWithoutExtension(node.Attribute("Include")!.Value.Replace('\\', '/'))).Order().ToArray();
            Assert.Equal(references.Order(), actual);
            if (project is "Crm.Domain" or "Crm.Application")
                Assert.DoesNotContain(document.Descendants("PackageReference"), node => node.Attribute("Include")!.Value.Contains("EntityFramework", StringComparison.Ordinal) || node.Attribute("Include")!.Value.Contains("AspNetCore", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Controllers_dispatch_through_MediatR_without_domain_or_persistence_calls()
    {
        var controllers = typeof(TicketsController).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ControllerBase))).ToArray();
        Assert.NotEmpty(controllers);
        foreach (var controller in controllers)
        {
            Assert.All(controller.GetConstructors(), constructor => Assert.Equal([typeof(ISender)], constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray()));
            // Include async state machines: dispatch occurs in their MoveNext bodies.
            var types = new[] { controller }.Concat(controller.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            var called = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .SelectMany(CalledMembers).ToArray();
            Assert.Contains(called, member => member.DeclaringType == typeof(ISender));
            Assert.DoesNotContain(called, member => ForbiddenControllerNamespace(member.DeclaringType?.Namespace));
        }
    }

    [Fact]
    public void Call_inspector_detects_a_domain_rule_in_a_controller_shaped_method()
    {
        var calls = CalledMembers(typeof(DependencyBoundaryTests).GetMethod(nameof(ForbiddenExample), BindingFlags.NonPublic | BindingFlags.Static)!);
        Assert.Contains(calls, member => ForbiddenControllerNamespace(member.DeclaringType?.Namespace));
    }

    private static string ForbiddenExample(Ticket ticket) => ticket.Reference;
    private static bool ForbiddenControllerNamespace(string? value) => value is not null && new[] { "Crm.Domain", "Crm.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient", "System.Data" }.Any(value.StartsWith);
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => code.Value);

    private static IEnumerable<MemberInfo> CalledMembers(MethodInfo method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null) yield break;
        for (var offset = 0; offset < bytes.Length;)
        {
            var first = bytes[offset++];
            var code = Codes[first == 0xfe ? (short)(0xfe00 | bytes[offset++]) : first];
            if (code.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                var member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset), method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                if (member is not null) yield return member;
            }
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4
            };
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crm.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }
}
