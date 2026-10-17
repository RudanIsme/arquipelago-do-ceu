using ArquipelagoDoCeu.Combat;
using Xunit;
namespace ArquipelagoDoCeu.Tests;

public class LetterCollectionTests
{
	[Fact] public void OrderedLettersComplete() { var c = new LetterCollection(); Assert.True(c.Place(1)); Assert.True(c.Place(2)); Assert.True(c.Place(0)); Assert.True(c.Complete); Assert.False(c.Place(3)); }
	[Fact] public void MistakeCanBeCorrected() { var c = new LetterCollection(); c.Place(1); c.Place(2); c.Place(3); Assert.False(c.Complete); Assert.True(c.Undo()); c.Place(0); Assert.True(c.Complete); }
	[Fact] public void InvalidAndDuplicateDoNotCount() { var c = new LetterCollection(); Assert.False(c.Place(-1)); Assert.False(c.Place(9)); c.Place(1); Assert.False(c.Place(1)); Assert.Single(c.Chosen); c.Reset(); Assert.Empty(c.Chosen); Assert.False(c.Undo()); Assert.False(c.Complete); }
}
