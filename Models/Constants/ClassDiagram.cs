// MIT License
// Copyright (c) 2025 Dave Black
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Models.Constants;

/// <summary>
/// Provides constants used for class diagram generation and related functionality.
/// </summary>
internal static class ClassDiagram
{
    internal const string Class = "class";
    internal const string Namespace = "namespace";
    internal const string Comment = "%%";
    internal const string Note = "note";
    internal const string Href = "href";

    /// <summary>
    /// Provides constant values representing common visibility modifiers used in UML diagrams and code modeling.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#visibility
    /// </summary>
    /// <remarks>These constants correspond to standard UML notation for visibility: "+" for public, "-" for
    /// private, "#" for protected, and "~" for package or internal visibility. They can be used when generating or
    /// interpreting UML diagrams or when modeling code elements programmatically.</remarks>
    internal static class Visibility
    {
        internal const string Public = "+";
        internal const string Private = "-";
        internal const string Protected = "#";
        internal const string PackageOrInternal = "~";
    }

    /// <summary>
    /// Provides constant string representations of common UML relationship types for use in diagram generation or parsing.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#defining-relationship
    /// </summary>
    /// <remarks>This class defines standard UML relationship symbols such as inheritance, composition,
    /// aggregation, association, dependency, and realization. The constants can be used to ensure consistency when
    /// working with UML diagrams or related tooling.</remarks>
    internal static class OneWayRelationship
    {
        internal const string Inheritance = "<|--";
        internal const string Composition = "*--";
        internal const string Aggregation = "o--";
        internal const string Association = "-->";
        internal const string LinkSolid = "--";
        internal const string Dependency = "..>";
        internal const string Realization = "..|>";
        internal const string LinkDashed = "..";
    }

    /// <summary>
    /// Provides constants that represent common two-way relationship symbols used in diagramming or modeling contexts.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#two-way-relations
    /// </summary>
    /// <remarks>These constants can be used to standardize the representation of relationships such as
    /// inheritance, composition, aggregation, association, and realization in diagrams or code generation tools. The
    /// class is intended for internal use and is not intended to be instantiated.</remarks>
    internal static class TwoWayRelationship
    {
        internal const string Inheritance = "<|";
        internal const string Composition = "\\*";
        internal const string Aggregation = "o";
        internal const string AssociationRight = ">";
        internal const string AssociationLeft = "<";
        internal const string Realization = "|>";
        internal const string LinkSolid = "--";
        internal const string LinkDashed = "..";
    }

    /// <summary>
    /// Provides constant string representations of common cardinality values used to describe the allowed number of
    /// occurrences in a relationship or collection.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#cardinality-multiplicity-on-relations
    /// </summary>
    /// <remarks>These constants are typically used to specify multiplicity constraints, such as in data
    /// modeling, validation, or schema definitions. The class is intended for internal use and is not intended to be
    /// instantiated.</remarks>
    internal static class Cardinality
    {
        internal const string One = "1";
        internal const string ZeroOrOne = "0..1";
        internal const string OneOrMore = "1..*";
        internal const string Many = "*";
        internal const string N = "n";
        internal const string ZeroToN = "0..n";
        internal const string OneToN = "1..n";
    }

    /// <summary>
    /// Provides constant string values used to annotate or categorize types, such as interfaces, abstract classes,
    /// services, and enumerations.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes
    /// </summary>
    /// <remarks>These annotation constants can be used to identify or label types according to their role or
    /// classification within a system. This class is intended for internal use and is not intended to be accessed
    /// directly by application code.</remarks>
    internal static class Annotation
    {
        internal const string Interface = "<<interface>>";
        internal const string Abstract = "<<abstract>>";
        internal const string Service = "<<service>>";
        internal const string Enumeration = "<<enumeration>>";
    }

    /// <summary>
    /// Provides constant values that represent supported interaction types for use with Mermaid.js class diagrams.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#interaction
    /// </summary>
    /// <remarks>These constants correspond to interaction types recognized by Mermaid.js, such as callbacks,
    /// clicks, and links. They can be used to specify or compare interaction types when generating or processing
    /// Mermaid.js diagrams programmatically.</remarks>
    internal static class Interaction
    {
        internal const string Callback = "callback";
        internal const string Click = "click";
        internal const string Link = "link";
    }

    /// <summary>
    /// Provides constant values that represent type modifiers used for internal metadata or code analysis.
    /// See: https://mermaid.js.org/syntax/classDiagram.html#visibility
    /// </summary>
    /// <remarks>This class is intended for internal use to standardize the representation of type modifiers
    /// such as abstract and static. The constants may be used as symbolic markers in code generation or analysis
    /// scenarios.</remarks>
    internal static class TypeModifiers
    {
        internal const string Abstract = "*";
        internal const string Static = "$";
        internal const string Generic = "~";
    }

    /// <summary>
    /// Provides constant string representations of common native .NET types for use in type mapping or interop
    /// scenarios. This is not an exhaustive list, but includes frequently used types.
    /// </summary>
    /// <remarks>This class is intended for internal use where type names need to be referenced as strings,
    /// such as in code generation, serialization, or interoperability layers. The constants correspond to frequently
    /// used .NET primitive and system types.</remarks>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Naming follows .NET type names.")]
    internal static class NativeType
    {
        internal const string ArrayBracketNotation = "[]";
        internal const string BigInteger = "BigInteger";
        internal const string Bool = "bool";
        internal const string Byte = "byte";
        internal const string CancellationToken = "CancellationToken";
        internal const string Char = "char";
        internal const string DateOnly = "DateOnly";
        internal const string DateTime = "DateTime";
        internal const string DateTimeOffset = "DateTimeOffset";
        internal const string Decimal = "decimal";
        internal const string Double = "double";
        internal const string Enum = "enum";
        internal const string Exception = "Exception";
        internal const string FileStream = "FileStream";
        internal const string Float = "float";
        internal const string Guid = "Guid";
        internal const string Half = "Half";
        internal const string IAsyncDisposable = "IAsyncDisposable";
        internal const string IDisposable = "IDisposable";
        internal const string Int = "int";
        internal const string Long = "long";
        internal const string MemoryOfT = $"Memory{TypeModifiers.Generic}";
        internal const string MemoryStream = "MemoryStream";
        internal const string NullableOfT = $"Nullable{TypeModifiers.Generic}";
        internal const string Object = "object";
        internal const string ReadOnlyMemoryOfT = $"ReadOnlyMemory{TypeModifiers.Generic}";
        internal const string ReadOnlySpanOfT = $"ReadOnlySpan{TypeModifiers.Generic}";
        internal const string SByte = "sbyte";
        internal const string Short = "short";
        internal const string SpanOfT = $"Span{TypeModifiers.Generic}";
        internal const string Stream = "Stream";
        internal const string String = "string";
        internal const string StringBuilder = "StringBuilder";
        internal const string Struct = "struct";
        internal const string Task = "Task";
        internal const string TaskOfT = $"Task{TypeModifiers.Generic}";
        internal const string TimeOnly = "TimeOnly";
        internal const string TimeSpan = "TimeSpan";
        internal const string Type = "Type";
        internal const string Uri = "Uri";
        internal const string ValueTask = "ValueTask";
        internal const string ValueTaskOfT = $"ValueTask{TypeModifiers.Generic}";
        internal const string Void = "void";

        // Unsigned types
        internal const string UInt = "uint";
        internal const string ULong = "ulong";
        internal const string UShort = "ushort";

        // Http types
        internal const string HttpClient = "HttpClient";
        internal const string HttpResponseMessage = "HttpResponseMessage";
        internal const string HttpRequestMessage = "HttpRequestMessage";
        internal const string HttpContent = "HttpContent";
        internal const string HttpMethod = "HttpMethod";
        internal const string HttpStatusCode = "HttpStatusCode";

        // LINQ types
        internal const string IOrderedQueryable = "IOrderedQueryable";
        internal const string IOrderedQueryableOfT = $"IOrderedQueryable{TypeModifiers.Generic}";
        internal const string Enumerable = "Enumerable";

        // LINQ interface types
        internal const string IAsyncEnumeratorOfT = $"IAsyncEnumerator{TypeModifiers.Generic}";
        internal const string IEnumerator = "IEnumerator";
        internal const string IEnumeratorOfT = $"IEnumerator{TypeModifiers.Generic}";
        internal const string IOrderedEnumerableOfT = $"IOrderedEnumerable{TypeModifiers.Generic}";

        // Generic collection/interface variants (single-generic-parameter forms)
        internal const string IAsyncEnumerableOfT = $"IAsyncEnumerable{TypeModifiers.Generic}";
        internal const string ICollection = "ICollection";
        internal const string ICollectionOfT = $"ICollection{TypeModifiers.Generic}";
        internal const string IEnumerable = "IEnumerable";
        internal const string IEnumerableOfT = $"IEnumerable{TypeModifiers.Generic}";
        internal const string IList = "IList";
        internal const string IListOfT = $"IList{TypeModifiers.Generic}";
        internal const string IQueryable = "IQueryable";
        internal const string IQueryableOfT = $"IQueryable{TypeModifiers.Generic}";
        internal const string IReadOnlyCollectionOfT = $"IReadOnlyCollection{TypeModifiers.Generic}";
        internal const string IReadOnlyListOfT = $"IReadOnlyList{TypeModifiers.Generic}";

        // Non-generic collection types
        internal const string Array = "Array";
        internal const string ArrayList = "ArrayList";
        internal const string Collection = "Collection";
        internal const string FrozenSet = "FrozenSet";
        internal const string Hashtable = "Hashtable";

        // Generic collection types (single-generic-parameter forms)
        internal const string ArrayOfT = $"Array{TypeModifiers.Generic}";
        internal const string ArraySegmentOfT = $"ArraySegment{TypeModifiers.Generic}";
        internal const string CollectionOfT = $"Collection{TypeModifiers.Generic}";
        internal const string FrozenSetOfT = $"FrozenSet{TypeModifiers.Generic}";
        internal const string HashSetOfT = $"HashSet{TypeModifiers.Generic}";
        internal const string LinkedListOfT = $"LinkedList{TypeModifiers.Generic}";
        internal const string ListOfT = $"List{TypeModifiers.Generic}";
        internal const string QueueOfT = $"Queue{TypeModifiers.Generic}";
        internal const string SortedSetOfT = $"SortedSet{TypeModifiers.Generic}";
        internal const string StackOfT = $"Stack{TypeModifiers.Generic}";
    }

    /// <summary>
    /// Provides constant keys for configuration options used within the application.
    /// See: https://mermaid.js.org/config/schema-docs/config-defs-class-diagram-config.html
    /// </summary>
    /// <remarks>This class is intended for internal use and supplies string constants that represent
    /// configuration property names. These keys are typically used to access or set configuration values in related
    /// components. The class is static and cannot be instantiated.</remarks>
    internal static class Config
    {
        internal const string TitleTopMargin = "titleTopMargin:";
        internal const string ArrowMarkerAbsolute = "arrowMarkerAbsolute:";
        internal const string DividerMargin = "dividerMargin:";
        internal const string Padding = "padding:";
        internal const string TextHeight = "textHeight:";
        internal const string DefaultRenderer = "defaultRenderer:";
        internal const string NodeSpacing = "nodeSpacing:";
        internal const string RankSpacing = "rankSpacing:";
        internal const string DiagramPadding = "diagramPadding:";
        internal const string HtmlLabels = "htmlLabels:";
        internal const string HideEmptyMembersBox = "hideEmptyMembersBox:";
    }
}
