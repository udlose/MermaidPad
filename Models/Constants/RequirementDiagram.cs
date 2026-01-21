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

namespace MermaidPad.Models.Constants;

/// <summary>
/// Provides constants and related functionality for working with requirement diagrams and their block types.
/// </summary>
/// <remarks>The RequirementDiagram class contains nested types and members that define string constants for
/// supported requirement block categories, such as functional, performance, interface, and physical requirements. Use
/// these constants to ensure consistent identification and handling of requirement block types throughout the
/// application.</remarks>
internal static class RequirementDiagram
{
    /// <summary>
    /// Provides constant string values that represent the supported types of requirement blocks.
    /// </summary>
    /// <remarks>These constants are used to identify different categories of requirements, such as functional,
    /// performance, interface, and physical requirements, as well as design constraints and elements. Use these values when
    /// working with requirement block types to ensure consistency across the application.</remarks>
    internal static class BlockTypes
    {
        internal const string Requirement = "requirement";
        internal const string FunctionalRequirement = "functionalRequirement";
        internal const string PerformanceRequirement = "performanceRequirement";
        internal const string InterfaceRequirement = "interfaceRequirement";
        internal const string PhysicalRequirement = "physicalRequirement";
        internal const string DesignConstraint = "designConstraint";
        internal const string Element = "element";
    }
}
