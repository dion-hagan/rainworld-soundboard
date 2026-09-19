using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    public class MiniYamlTests
    {
        private static string Scalar(YamlNode node) => node.Text;

        [Fact]
        public void EmptyDocumentIsEmptyMapping()
        {
            YamlNode root = MiniYaml.Parse("  \n# just a comment\n\n");
            Assert.Equal(YamlKind.Mapping, root.Kind);
            Assert.Empty(root.Entries);
        }

        [Fact]
        public void ParsesNestedMappingsAndLists()
        {
            YamlNode root = MiniYaml.Parse(
                "events:\n" +
                "  PlayerDeath:\n" +
                "    - boom.wav\n" +
                "    - file: bang.wav\n" +
                "      volume: 0.5\n" +
                "  PlayerJump:\n" +
                "    - jump.wav\n");

            YamlNode death = root.Find("events").Value.Find("PlayerDeath").Value;
            Assert.Equal(YamlKind.Sequence, death.Kind);
            Assert.Equal(2, death.Items.Count);
            Assert.Equal("boom.wav", Scalar(death.Items[0]));
            Assert.Equal("bang.wav", Scalar(death.Items[1].Find("file").Value));
            Assert.Equal("0.5", Scalar(death.Items[1].Find("volume").Value));
            Assert.Equal("jump.wav", Scalar(root.Find("events").Value.Find("PlayerJump").Value.Items[0]));
        }

        [Fact]
        public void ListMayStartAtTheSameIndentAsItsKey()
        {
            YamlNode root = MiniYaml.Parse("a:\n- one\n- two\nb: x\n");
            Assert.Equal(new[] { "one", "two" }, root.Find("a").Value.Items.Select(Scalar));
            Assert.Equal("x", Scalar(root.Find("b").Value));
        }

        [Fact]
        public void RecordsLineNumbers()
        {
            YamlNode root = MiniYaml.Parse("# c\na: 1\n\nb:\n  - x\n");
            Assert.Equal(2, root.Find("a").Line);
            Assert.Equal(4, root.Find("b").Line);
            Assert.Equal(5, root.Find("b").Value.Items[0].Line);
        }

        [Fact]
        public void StripsCommentsButKeepsHashesInsideQuotesAndWords()
        {
            YamlNode root = MiniYaml.Parse(
                "a: 1 # note\n" +
                "b: \"x # not a comment\"\n" +
                "c: 'y # nor this'\n" +
                "d: C#sharp\n" +
                "e: don't stop # but this goes\n");

            Assert.Equal("1", Scalar(root.Find("a").Value));
            Assert.Equal("x # not a comment", Scalar(root.Find("b").Value));
            Assert.Equal("y # nor this", Scalar(root.Find("c").Value));
            Assert.Equal("C#sharp", Scalar(root.Find("d").Value));
            Assert.Equal("don't stop", Scalar(root.Find("e").Value));
        }

        [Fact]
        public void QuotedScalarsAreUnescaped()
        {
            YamlNode root = MiniYaml.Parse("a: \"tab\\there \\\"q\\\" \\\\ \\u0041\"\nb: 'it''s'\nc: \"key: value\"\n");
            Assert.Equal("tab\there \"q\" \\ A", Scalar(root.Find("a").Value));
            Assert.Equal("it's", Scalar(root.Find("b").Value));
            Assert.Equal("key: value", Scalar(root.Find("c").Value));
        }

        [Fact]
        public void NullValues()
        {
            YamlNode root = MiniYaml.Parse("a:\nb: ~\nc: null\nd: ''\n");
            Assert.True(root.Find("a").Value.IsNull);
            Assert.True(root.Find("b").Value.IsNull);
            Assert.True(root.Find("c").Value.IsNull);
            Assert.False(root.Find("d").Value.IsNull); // an explicitly empty string is not null
        }

        [Fact]
        public void InlineListsAndMaps()
        {
            YamlNode root = MiniYaml.Parse("a: [x, 'y, z', \"w\"]\nb: { file: boom.wav, volume: 0.5 }\nc: []\nd: {}\n");
            Assert.Equal(new[] { "x", "y, z", "w" }, root.Find("a").Value.Items.Select(Scalar));
            Assert.Equal("boom.wav", Scalar(root.Find("b").Value.Find("file").Value));
            Assert.Equal("0.5", Scalar(root.Find("b").Value.Find("volume").Value));
            Assert.Empty(root.Find("c").Value.Items);
            Assert.Empty(root.Find("d").Value.Entries);
        }

        [Fact]
        public void InlineMapInsideListItem()
        {
            YamlNode root = MiniYaml.Parse("events:\n  PlayerDeath:\n    - { file: a.wav, volume: 0.2 }\n    - b.wav\n");
            YamlNode items = root.Find("events").Value.Find("PlayerDeath").Value;
            Assert.Equal("a.wav", Scalar(items.Items[0].Find("file").Value));
            Assert.Equal("b.wav", Scalar(items.Items[1]));
        }

        [Fact]
        public void InlineCollectionsMaySpanLines()
        {
            YamlNode root = MiniYaml.Parse("a: [\n  one,\n  two\n]\nb: 1\n");
            Assert.Equal(new[] { "one", "two" }, root.Find("a").Value.Items.Select(Scalar));
            Assert.Equal("1", Scalar(root.Find("b").Value));
        }

        [Fact]
        public void ListItemMappingContinuesOnFollowingLines()
        {
            YamlNode root = MiniYaml.Parse(
                "- together:\n" +
                "    - a.wav\n" +
                "    - file: b.wav\n" +
                "      volume: 2\n" +
                "  volume: 0.5\n" +
                "- c.wav\n");

            YamlNode first = root.Items[0];
            Assert.Equal(2, first.Find("together").Value.Items.Count);
            Assert.Equal("0.5", Scalar(first.Find("volume").Value));
            Assert.Equal("c.wav", Scalar(root.Items[1]));
        }

        [Fact]
        public void KeysMayContainSpacesAndBeQuoted()
        {
            YamlNode root = MiniYaml.Parse("player death: x\n\"odd: key\": y\n");
            Assert.Equal("x", Scalar(root.Find("player death").Value));
            Assert.Equal("y", Scalar(root.Find("odd: key").Value));
        }

        [Fact]
        public void HandlesWindowsLineEndingsAndBom()
        {
            YamlNode root = MiniYaml.Parse("\uFEFFa: 1\r\nb:\r\n  - x\r\n");
            Assert.Equal("1", Scalar(root.Find("a").Value));
            Assert.Equal("x", Scalar(root.Find("b").Value.Items[0]));
        }

        [Fact]
        public void LeadingDocumentMarkerIsAllowed()
        {
            YamlNode root = MiniYaml.Parse("---\na: 1\n");
            Assert.Equal("1", Scalar(root.Find("a").Value));
        }

        // --- errors: every one should say which line and how to fix it -------

        private static YamlParseException Fails(string yaml)
        {
            return Assert.Throws<YamlParseException>(() => MiniYaml.Parse(yaml));
        }

        [Fact]
        public void DuplicateKeysAreReportedWithBothLines()
        {
            YamlParseException e = Fails("a: 1\nb: 2\na: 3\n");
            Assert.Equal(3, e.Line);
            Assert.Contains("line 1", e.Message);
        }

        [Fact]
        public void TabIndentationIsRejectedClearly()
        {
            YamlParseException e = Fails("a:\n\t- x\n");
            Assert.Equal(2, e.Line);
            Assert.Contains("TAB", e.Message);
        }

        [Fact]
        public void BlankAndCommentOnlyTabLinesAreFine()
        {
            MiniYaml.Parse("a: 1\n\t\n\t# comment\nb: 2\n");
        }

        [Fact]
        public void MissingColonIsReported()
        {
            YamlParseException e = Fails("a: 1\njust some text\n");
            Assert.Equal(2, e.Line);
            Assert.Contains("colon", e.Message);
        }

        [Fact]
        public void UnclosedQuoteIsReported()
        {
            YamlParseException e = Fails("a: \"oops\n");
            Assert.Equal(1, e.Line);
            Assert.Contains("never closed", e.Message);
        }

        [Fact]
        public void UnclosedBracketIsReported()
        {
            YamlParseException e = Fails("a: [x, y\nb: 1\n");
            Assert.Equal(1, e.Line);
        }

        [Fact]
        public void TextAfterClosingQuoteIsReported()
        {
            Fails("a: \"x\" y\n");
        }

        [Fact]
        public void ValueContinuedOnNextLineIsRejected()
        {
            YamlParseException e = Fails("a: some text\n   more text\n");
            Assert.Equal(2, e.Line);
            Assert.Contains("one line", e.Message);
        }

        [Fact]
        public void OverIndentedLineIsRejected()
        {
            YamlParseException e = Fails("a: 1\n    b: 2\n");
            Assert.Equal(2, e.Line);
        }

        [Fact]
        public void InconsistentIndentIsRejected()
        {
            Fails("a:\n    b: 1\n  c: 2\n");
        }

        [Fact]
        public void ListItemWithoutAKeyIsRejected()
        {
            YamlParseException e = Fails("a: 1\n- x\n");
            Assert.Equal(2, e.Line);
        }

        [Fact]
        public void UnsupportedYamlFeaturesGetClearMessages()
        {
            Assert.Contains("aren't supported", Fails("a: &anchor x\n").Message);
            Assert.Contains("Multi-line", Fails("a: |\n  text\n").Message);
            Assert.Contains("one YAML document", Fails("a: 1\n---\nb: 2\n").Message);
        }

        [Fact]
        public void BadEscapeInDoubleQuotesSuggestsSingleQuotes()
        {
            YamlParseException e = Fails("a: \"C:\\sounds\\boom.wav\"\n");
            Assert.Contains("single quotes", e.Message);
        }
    }
}
