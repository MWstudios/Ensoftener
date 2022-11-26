using System;
using System.IO;
using System.Xml;
using System.Text;
/*using System.Net;
using System.Timers;
using System.Windows;
using System.Numerics;
using System.Data.Odbc;
using System.Reflection;
using System.Collections;
using System.Text.Unicode;
using System.Configuration;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.DirectoryServices;
using System.IO.IsolatedStorage;
using System.Collections.Generic;
using System.Resources.Extensions;
using System.Collections.Immutable;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Configuration.Assemblies;
using System.Runtime.ExceptionServices;
using System.Windows.Forms.VisualStyles;
using System.Diagnostics.PerformanceData;
using System.Net.PeerToPeer.Collaboration;
using System.Reflection.PortableExecutable;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.ComTypes;
using System.DirectoryServices.ActiveDirectory;
using System.ComponentModel.Design.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms.ComponentModel.Com2Interop;
using System.Security.Authentication.ExtendedProtection;*/ //noice
using System.Collections.Generic;

using SharpDX;
using SharpDX.Direct2D1;

namespace Ensoftener
{
    /// <summary>A SVG image that can be modified at runtime.</summary>
    /// <remarks>If you're planning to apply position, rotation and scale to your SVG, make sure that all shapes in the SVG are grouped by &lt;g&gt; elements.
    /// Some editors don't group them by default, in which case you have to group them. On the other hand, make sure the editor didn't already apply a transform to the groups,
    /// because that's going to be overriden.</remarks>
    public class SvgImage : IDisposable
    {
        float x, y, rotation, width = 1, height = 1; Matrix3x2 matrix = new(); bool disposedValue, outdated; XmlDocument DocumentAsXml; readonly DeviceContext5 DeviceContext;
        public SvgElement1 Root { get; private set; } public SvgDocument Document { get; private set; }
        /// <summary>Rebuilds the SVG everytime the Outdated is set to true.</summary>
        public bool UpdateIfOutdated { get; set; }
        /// <summary>Direct2D-controlled SVG elements are less accessible than in a normal XML parser, and therefore can go out of sync.
        /// This flag indicates that the SVG needs to be recreated.</summary>
        public bool Outdated { get => outdated; private set { outdated = value; if (UpdateIfOutdated && value) Rebuild(); } }
        /// <summary>X position of the SVG.</summary>
        public float X { get => x; set { x = value; SetTranslation(); } }
        /// <summary>Y position of the SVG.</summary>
        public float Y { get => y; set { y = value; SetTranslation(); } }
        /// <summary>Rotation of the SVG, clockwise, in degrees.</summary>
        public float Rotation { get => rotation; set { rotation = value; SetTranslation(); } }
        /// <summary>Width <b>multiplier</b> of the SVG.</summary>
        public float Width { get => width; set { width = value; SetTranslation(); } }
        /// <summary>Height <b>multiplier</b> of the SVG.</summary>
        public float Height { get => height; set { height = value; SetTranslation(); } }
        /// <summary>Transform matrix of the SVG. This applies on top of the other translations.</summary>
        public Matrix3x2 Matrix { get => matrix; set { matrix = value; SetTranslation(); } }
        void SetTranslation()
        {
            var dP = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var e in Root.SubElements) if (e.Name == "g") e["transform"] =
                    $"translate({x.ToString(dP)},{y.ToString(dP)}) rotate({rotation.ToString(dP)}) scale({Width.ToString(dP)},{Height.ToString(dP)})"
                    + $" matrix({matrix.M11.ToString(dP)}, {matrix.M12.ToString(dP)}, {matrix.M21.ToString(dP)},"
                    + $" {matrix.M22.ToString(dP)}, {matrix.M31.ToString(dP)}, {matrix.M32.ToString(dP)})";
        }
        /// <summary>Creates an SVG image from a file or an XML string.</summary>
        /// <param name="input">The file path or XML string.</param>
        /// <param name="fromFile">Determines whether to read from a file or from the string itself.</param>
        public SvgImage(DeviceContext d2dc, string input, bool fromFile = true)
        {
            DeviceContext = d2dc.QueryInterface<DeviceContext5>();
            Rebuild(fromFile ? File.ReadAllText(input) : input);
        }
        /// <summary>Recreates the SVG image. See the description of the <b><seealso cref="Outdated"/></b> property for why this needs to be done.
        /// <br/>This method is automatically called when <b><seealso cref="UpdateIfOutdated"/></b> is set to true.</summary>
        public void Rebuild() => Rebuild(DocumentAsXml.OuterXml);
        public void Rebuild(string XML)
        {
            using MemoryStream readStream = new(Encoding.Default.GetBytes(XML));
            using SharpDX.WIC.WICStream wicStream = new(GDX.WICFactory, readStream);
            Document?.Dispose();
            Document = DeviceContext.CreateSvgDocument(wicStream, new(GDX.Form.Size.Width, GDX.Form.Size.Height));
            DocumentAsXml = new(); DocumentAsXml.LoadXml(XML);
            Root?.Dispose(); Root = new(this, null, Document.Root, DocumentAsXml["svg"]); Outdated = false;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Root.Dispose(); Document.Dispose();
                    // TODO: Uvolněte spravovaný stav (spravované objekty).
                }
                // TODO: Uvolněte nespravované prostředky (nespravované objekty) a přepište finalizační metodu.
                // TODO: Nastavte velká pole na hodnotu null.
                disposedValue = true;
            }
        }
        // TODO: Finalizační metodu přepište, jen pokud metoda Dispose(bool disposing) obsahuje kód pro uvolnění nespravovaných prostředků.
        ~SvgImage() => Dispose(disposing: false);
        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
        public class SvgElement1 : IDisposable
        {
            readonly XmlElement svgToXml; readonly SvgElement Element; bool disposedValue;
            public SvgImage OwnerImage { get; } public SvgElement1 Owner { get; } public string Name => svgToXml.Name;
            public string InlineText { get => svgToXml.InnerText; set { svgToXml.InnerText = value; Element.SetTextValue(value, 1); } }
            public List<SvgElement1> SubElements { get; private set; } = new();
            /// <summary>Gets all attribute names that exist in this element.</summary>
            public List<string> Attributes { get; private set; } = new();
            public SvgElement1(SvgImage ownerImage, SvgElement1 owner, SvgElement element, XmlElement xmlCounterpart)
            {
                OwnerImage = ownerImage; Owner = owner; Element = element; svgToXml = xmlCounterpart;
                foreach (XmlAttribute node in svgToXml.Attributes) Attributes.Add(node.Name); int i = 0;
                foreach (XmlNode child in svgToXml.ChildNodes) if (child is XmlElement xE && !IgnoreElement(svgToXml, xE))
                    { SubElements.Add(new(ownerImage, this, Element.Children[i], xE)); i++; }
            }
            static bool IgnoreElement(XmlElement owner, XmlElement child) => owner.Name switch
            {
                "defs" => child.Name switch { "bx:grid" => true, _ => false },
                _ => false
            };
            /// <summary>Some elements may contain an attribute (such as "style") that packs multiple attributes into one. This method dissects it back.</summary>
            /// <param name="key">The attribute to be dissected.</param>
            /// <param name="overrideExisting">Override existing attributes by the new and dissected ones.</param>
            /// <remarks>This method outdates the image.</remarks>
            public void DissectAttribute(string key, bool overrideExisting = true)
            {
                if (!Attributes.Contains(key)) return; string origin = this[key], rKey = string.Empty, rValue;
                int index = SkipSpaces(origin, 0); RemoveAttribute(key);
                for (int i = index; i < origin.Length; ++i) switch (origin[i])
                    {
                        case ':': rKey = origin[index..i]; i = SkipSpaces(origin, i + 1); index = i; break;
                        case ';': rValue = origin[index..i]; if (overrideExisting || !svgToXml.HasAttribute(rKey)) this[rKey] = rValue;
                            i = SkipSpaces(origin, i + 1); index = i; break;
                        default: break;
                    }
            }
            static int SkipSpaces(string text, int index) { for (; index < text.Length; index++) if (text[index] != ' ') break; return index; }
            /// <summary>Gets or sets an attribute by its name.</summary>
            /// <remarks>This method outdates the image.</remarks>
            public string this[string key]
            {
                get => svgToXml.GetAttribute(key); set
                {
                    svgToXml.SetAttribute(key, value);
                    if (!Attributes.Contains(key)) { Attributes.Add(key); OwnerImage.Outdated = true; }
                    else Element.SetAttributeValue(key, SvgAttributeStringType.Svg, value);
                }
            }
            /// <summary>Gets an attribute by its name and returns <paramref name="defaultValue"/> if nothing is found.</summary>
            /// <remarks>If you're trying to get a shape's coordinates, you might want to use this with <paramref name="defaultValue"/> set to 0.
            /// Some SVG editors like Boxy leave out this attribute if the shape is located at (0, 0) and same thing could happen in other cases as well.</remarks>
            public string this[string key, string defaultValue]
            { get { string result = svgToXml.GetAttribute(key); return string.IsNullOrEmpty(result) ? defaultValue : result; } }
            public void RemoveAttribute(string key)
            { svgToXml.RemoveAttribute(key); if (Element.IsAttributeSpecified(key, out _)) Element.RemoveAttribute(key); Attributes.Remove(key); }
            public void AddElement(string name)
            {
                XmlElement XMLE = OwnerImage.DocumentAsXml.CreateElement(name);
                svgToXml.AppendChild(XMLE); Element.CreateChild(name, out SvgElement SVGE);
                SubElements.Add(new(OwnerImage, this, SVGE, XMLE));
            }
            public void RemoveElement(string name) => RemoveElement(SubElements.Find(x => x.Name == name));
            public void RemoveElement(SvgElement1 element)
            { if (element == null) return; svgToXml.RemoveChild(element.svgToXml); Element.RemoveChild(element.Element); element.Dispose(); SubElements.Remove(element); }
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        foreach (var child in SubElements) child.Dispose(); Element.Dispose();
                        // TODO: Uvolněte spravovaný stav (spravované objekty).
                    }
                    // TODO: Uvolněte nespravované prostředky (nespravované objekty) a přepište finalizační metodu.
                    // TODO: Nastavte velká pole na hodnotu null.
                    disposedValue = true;
                }
            }
            // TODO: Finalizační metodu přepište, jen pokud metoda Dispose(bool disposing) obsahuje kód pro uvolnění nespravovaných prostředků.
            ~SvgElement1() => Dispose(disposing: false);
            public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
        }
    }
}
