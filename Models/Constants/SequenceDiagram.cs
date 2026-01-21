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

internal static class SequenceDiagram
{
    internal static class ParticipantTypes
    {
        internal const string Actor = "actor";
        internal const string Participant = "participant";
        internal const string Boundary = "boundary";
        internal const string Control = "control";
        internal const string Entity = "entity";
        internal const string Database = GeneralElementNames.Database;
    }

    internal static class BlockOpenerNames
    {
        internal const string Loop = "loop";
        internal const string Alt = "alt";
        internal const string Else = "else";
        internal const string Opt = "opt";
        internal const string Par = "par";
        internal const string And = "and";
        internal const string Critical = "critical";
        internal const string Break = "break";
        internal const string Rect = ShapeNames.Rect;
    }
}
