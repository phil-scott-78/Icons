using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace IconSourceGen;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        File.AppendAllText("r:\\output2.txt", "init");


        var config = GetConfigValue(context);
        var namesAndContents = context.AdditionalTextsProvider
            .Combine(config)
            .Where(static value => value.Left.Path.EndsWith(".svg"))
            .Select(static (value, token) => (Config: value.Right, Path: value.Left.Path,
                Content: value.Left.GetText(token)!.ToString()))
            .Select(static (value, token) => ParseSvg(value))
            .Where(i => i != null);

        context.RegisterSourceOutput(config, AddDefaultClasses);
        context.RegisterSourceOutput(namesAndContents, Execute);
    }

    private void AddDefaultClasses(SourceProductionContext context, Config config)
    {
        var src = $$""" 
#nullable enable
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace {{ config.RootNameSpace}} 
{
    public class Icon : ComponentBase
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? AdditionalAttributes { get; set; }

        [Parameter, EditorRequired]
        public IconData? Item { get; set; } = null!;
    
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            string? cssClass = null;
            if (AdditionalAttributes?.TryGetValue("class", out var classObject) == true)
            {
                cssClass = classObject.ToString();
            }

            #nullable disable
            builder.OpenElement(0, "svg");
            builder.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
            builder.AddAttribute(2, "class", cssClass);
            var viewBox = Item.ViewBox;
            var content = Item.Content;
            builder.AddAttribute(3, "viewBox", viewBox);
            builder.AddMultipleAttributes(4, AdditionalAttributes?.Where(i => i.Key != "class" && i.Value is not IconData) );
            builder.AddMarkupContent(5, content);
            builder.CloseElement();
        }
    }

    public class IconData
    {
        public string Content { get; init; }
        public string ViewBox { get; init; }
    }
}
""" ;

        context.AddSource("component-and-data.g.cs", src);
    }

    private static void Execute(SourceProductionContext spc,
        (string ns, string className, string iconName, string viewBox, string content)? value)
    {
        if (value == null) return;

        var (ns, className, iconName, viewBox, content) = value.Value;

        var source = $$$"""
namespace {{{ ns}}}  
{
    public static partial class {{{ className}}} 
    {
        public static IconData {{{ iconName}}} () => new()
        {
            Content = "{{{ content.Replace("\"", "\\\"")}}}",
            ViewBox = "{{{ viewBox}}} "
        };
    }
}
""" ;

        spc.AddSource($"{ns}-{className}-{iconName}.g.cs", source);
    }


    private static (string ns, string className, string iconName, string viewBox, string content)? ParseSvg(
        (Config Config, string Path, string Content) value)
    {
        var path = Path.GetDirectoryName(value.Path);
        if (path == null)
        {
            return null;
        }

        var parentFolder = Directory.GetParent(path);
        while (parentFolder != null && parentFolder.Name.ToLower() != value.Config.RootFolder)
        {
            parentFolder = parentFolder.Parent;
        }

        if (parentFolder == null)
        {
            return null;
        }

        path = path.Replace(parentFolder.FullName, string.Empty);
        if (path.StartsWith("/") || path.StartsWith("\\"))
        {
            path = path.Substring(1);
        }

        var absolutePath = value.Path.Replace(parentFolder.FullName, string.Empty);
        var fileName = Path.GetFileNameWithoutExtension(absolutePath);
        var doc = new XmlDocument();
        doc.LoadXml(value.Content);
        var node = doc.FirstChild;
        if (node.Attributes == null)
        {
            return null;
        }

        var viewBox = node.Attributes["viewBox"].Value;
        // ok, this is beyond naive but whatever.
        var content = node.InnerXml;
        var className = path.Replace("/", ".").Replace("\\", ".");
        className = string.Join("",
            className.Split('.').Select(i => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(i)).ToArray());
        var iconName = GenerateIconName(fileName);

        return (value.Config.RootNameSpace, className, iconName, viewBox, content);
    }

    private static IncrementalValueProvider<Config> GetConfigValue(
        IncrementalGeneratorInitializationContext context)
    {
        var config = context.AnalyzerConfigOptionsProvider.Select((provider, _) =>
        {
            var ns = "BadIdeas.FontAwesome.Icons";
            var rootFolder = "svgs";

            if (provider.GlobalOptions.TryGetValue("icon_gen_root_namespace", out var configValue))
            {
                ns = configValue;
            }

            if (provider.GlobalOptions.TryGetValue("icon_gen_root_folder", out var rootFolderValue))
            {
                rootFolder = rootFolderValue;
            }

            return new Config(ns, rootFolder);
        });
        return config;
    }

    private static string GenerateIconName(string value)
    {
        var className = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
        className = Regex.Replace(className, @"[^\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}\p{Nl}\p{Mn}\p{Mc}\p{Cf}\p{Pc}\p{Lm}]",
            "");

        // Class name doesn't begin with a letter, insert an underscore
        if (!char.IsLetter(className, 0))
        {
            className = className.Insert(0, "_");
        }

        className = className.Replace(" ", string.Empty);
        return className;
    }
}