using System;
using System.Text;

namespace SuperSocket.Command.SourceGeneration.Emit;

internal sealed class SourceBuilder
{
    private const string IndentText = "    ";
    private readonly StringBuilder _builder = new();
    private int _indent;

    public SourceBuilder AppendLine()
    {
        _builder.AppendLine();
        return this;
    }

    public SourceBuilder AppendLine(string value)
    {
        if (value.Length > 0)
        {
            for (int i = 0; i < _indent; i++)
            {
                _builder.Append(IndentText);
            }
        }

        _builder.AppendLine(value);
        return this;
    }

    public IDisposable Indent()
    {
        _indent++;
        return new IndentScope(this);
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void Unindent()
    {
        _indent--;
    }

    private sealed class IndentScope(SourceBuilder builder) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            builder.Unindent();
            _disposed = true;
        }
    }
}
