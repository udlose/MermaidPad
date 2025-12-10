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
        public const string Actor = "actor";
        public const string Participant = "participant";
        public const string Boundary = "boundary";
        public const string Control = "control";
        public const string Entity = "entity";
        public const string Database = GeneralElementNames.Database;
    }

    internal static class BlockOpenerNames
    {
        public const string Loop = "loop";
        public const string Alt = "alt";
        public const string Else = "else";
        public const string Opt = "opt";
        public const string Par = "par";
        public const string And = "and";
        public const string Critical = "critical";
        public const string Break = "break";
        public const string Rect = ShapeNames.Rect;
    }
}
