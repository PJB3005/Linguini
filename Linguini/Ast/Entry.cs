﻿using System;
using System.Collections.Generic;
using System.Text;
using Linguini.Parser;

namespace Linguini.Ast
{
    public class Resource
    {
        public List<IEntry> Body;
        public List<ParseError> Errors;

        public Resource(List<IEntry> body, List<ParseError> errors)
        {
            Body = body;
            Errors = errors;
        }
    }

    public class Message : IEntry
    {
        public Identifier Id;
        public Pattern? Value;
        public List<Attribute> Attributes;
        public Comment? Comment;
    }

    public class Term : IEntry
    {
        public Identifier Id;
        public Pattern Value;
        public List<Attribute> Attributes;
        public Comment? Comment;
    }

    public class Comment : IEntry
    {
        public CommentLevel CommentLevel;
        private readonly List<ReadOnlyMemory<char>> _content;

        public Comment() : this(CommentLevel.Comment, new())
        {
        }

        public Comment(CommentLevel commentLevel, List<ReadOnlyMemory<char>> content)
        {
            CommentLevel = commentLevel;
            _content = content;
        }

        public string Content()
        {
            StringBuilder sb = new();
            for (int i = 0; i < _content.Count; i++)
            {
                sb.Append(_content[i].ToArray());
            }

            return sb.ToString();
        }
    }

    public class Junk : IEntry
    {
        public ReadOnlyMemory<char> Content;
        public string ContentStr()
        {
            return new(Content.ToArray());
        }
    }
}
