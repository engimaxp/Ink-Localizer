namespace InkLocalizer;

[Serializable]
internal class InkParsingException : Exception {
	public InkParsingException(){ }
	public InkParsingException(string message) : base(message) { }
	public InkParsingException(string message, Exception inner) : base(message, inner) { }
}