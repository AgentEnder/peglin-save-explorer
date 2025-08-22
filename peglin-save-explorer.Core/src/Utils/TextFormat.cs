using System;

namespace peglin_save_explorer.Utils
{
    public struct TextFormat : IEquatable<TextFormat>
    {
        public ConsoleColor ForegroundColor { get; }
        public ConsoleColor? BackgroundColor { get; }
        public bool HasBackground => BackgroundColor.HasValue;
        
        public TextFormat(ConsoleColor foregroundColor, ConsoleColor? backgroundColor = null)
        {
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
        }
        
        public static TextFormat Default => new TextFormat(ConsoleColor.Gray);
        public static TextFormat Selected => new TextFormat(ConsoleColor.Black, ConsoleColor.Gray);
        public static TextFormat Highlighted => new TextFormat(ConsoleColor.Yellow);
        public static TextFormat Error => new TextFormat(ConsoleColor.Red);
        public static TextFormat Success => new TextFormat(ConsoleColor.Green);
        
        public bool Equals(TextFormat other)
        {
            return ForegroundColor == other.ForegroundColor && BackgroundColor == other.BackgroundColor;
        }
        
        public override bool Equals(object obj)
        {
            return obj is TextFormat other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine((int)ForegroundColor, (int)BackgroundColor);
        }
        
        public static bool operator ==(TextFormat left, TextFormat right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(TextFormat left, TextFormat right)
        {
            return !left.Equals(right);
        }
    }

    public struct FormattedChar
    {
        public char Character { get; }
        public TextFormat Format { get; }
        
        public FormattedChar(char character, TextFormat format)
        {
            Character = character;
            Format = format;
        }
        
        public FormattedChar(char character, ConsoleColor foreground = ConsoleColor.Gray, ConsoleColor? background = null)
        {
            Character = character;
            Format = new TextFormat(foreground, background);
        }
    }

    public class FormattedString
    {
        private readonly FormattedChar[] characters;
        
        public int Length => characters.Length;
        
        public FormattedChar this[int index] => characters[index];
        
        public FormattedString(string text, TextFormat format)
        {
            characters = new FormattedChar[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                characters[i] = new FormattedChar(text[i], format);
            }
        }
        
        public FormattedString(string text, ConsoleColor foreground = ConsoleColor.Gray, ConsoleColor? background = null)
            : this(text, new TextFormat(foreground, background))
        {
        }
        
        public FormattedString(FormattedChar[] characters)
        {
            this.characters = new FormattedChar[characters.Length];
            Array.Copy(characters, this.characters, characters.Length);
        }
        
        public static implicit operator FormattedString(string text)
        {
            return new FormattedString(text, TextFormat.Default);
        }
        
        public override string ToString()
        {
            var chars = new char[characters.Length];
            for (int i = 0; i < characters.Length; i++)
            {
                chars[i] = characters[i].Character;
            }
            return new string(chars);
        }
    }
}