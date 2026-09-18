using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingotion.Thespeon.Samples.AdvancedGUI
{
    /// <summary>
    /// The subtitle-style overlay the Unreal demo shows while a line is being spoken: the speaker's portrait and
    /// name on the left, the line itself on the right, revealed as it is heard.
    ///
    /// The element only renders what it is told to. Which words have been reached is worked out from audio
    /// sample request markers by <see cref="AdvancedThespeonGUI"/>, which calls <see cref="SetProgress"/> every
    /// frame; the overlay turns that into a reveal.
    ///
    /// Text that has not been reached yet is still laid out, just drawn fully transparent, so the line does not
    /// reflow as it is revealed.
    /// </summary>
    public class KaraokeOverlay : VisualElement
    {
        private readonly VisualElement portrait;
        private readonly Label nameLabel;
        private readonly Label lineLabel;

        /// <summary>The words of the current line, in order. Empty until SetLine is called.</summary>
        private readonly List<string> words = new();

        /// <summary>
        /// Character offset in <see cref="plainText"/> where each word starts. One entry per word, plus a final
        /// entry holding the total length, so the span of word i is always [offsets[i], offsets[i + 1]).
        /// </summary>
        private readonly List<int> wordOffsets = new();

        private string plainText = string.Empty;
        private int revealedCharacters = -1;
        private bool isWaiting;

        public KaraokeOverlay()
        {
            AddToClassList("lt-karaoke");
            // Purely decorative: it must never eat clicks meant for the panel underneath.
            pickingMode = PickingMode.Ignore;

            VisualElement speaker = new();
            speaker.AddToClassList("lt-karaoke__speaker");
            Add(speaker);

            portrait = new VisualElement();
            portrait.AddToClassList("lt-karaoke__portrait");
            speaker.Add(portrait);

            nameLabel = new Label();
            nameLabel.AddToClassList("lt-karaoke__name");
            speaker.Add(nameLabel);

            VisualElement bubble = new();
            bubble.AddToClassList("lt-karaoke__bubble");
            Add(bubble);

            lineLabel = new Label();
            lineLabel.AddToClassList("lt-karaoke__line");
            lineLabel.enableRichText = true;
            bubble.Add(lineLabel);

            Hide();
        }

        /// <summary>Sets who is speaking. A null portrait leaves the frame empty rather than stretching a stale one.</summary>
        public void SetSpeaker(string characterName, Texture2D image)
        {
            nameLabel.text = characterName ?? string.Empty;
            if (image != null) portrait.style.backgroundImage = new StyleBackground(image);
            else portrait.style.backgroundImage = StyleKeyword.None;
        }

        /// <summary>
        /// Sets the line to reveal, as the words that were sent to Thespeon. Resets the reveal to nothing.
        /// </summary>
        public void SetLine(IReadOnlyList<string> lineWords)
        {
            words.Clear();
            wordOffsets.Clear();

            StringBuilder builder = new();
            if (lineWords != null)
            {
                foreach (string word in lineWords)
                {
                    if (string.IsNullOrEmpty(word)) continue;
                    if (builder.Length > 0) builder.Append(' ');

                    wordOffsets.Add(builder.Length);
                    words.Add(word);
                    builder.Append(word);
                }
            }

            plainText = builder.ToString();
            // Sentinel: lets SetProgress treat the last word like any other.
            wordOffsets.Add(plainText.Length);

            revealedCharacters = -1;
            Render(0);
        }

        /// <summary>
        /// Reveals the line to where the audio has got: the word at <paramref name="wordIndex"/> is the one being
        /// spoken and is shown whole, and <paramref name="wordFraction"/> - how far through that word the audio is
        /// - reveals that much of the word *after* it. A negative word index means nothing has been reached yet.
        ///
        /// Whole words are only ever revealed on their own marker, which is the timing Unreal's demo uses and the
        /// only part of it the engine actually tells us. The fraction is a linear approximation used solely to
        /// bring the next word in gradually across the current one, so it is complete just as it starts sounding
        /// rather than snapping into place a word at a time.
        /// </summary>
        public void SetProgress(int wordIndex, float wordFraction)
        {
            if (words.Count == 0) return;

            if (wordIndex < 0)
            {
                Render(0);
                return;
            }

            wordIndex = Mathf.Min(wordIndex, words.Count - 1);
            int spokenEnd = wordOffsets[wordIndex] + words[wordIndex].Length;
            if (wordIndex + 1 >= words.Count)
            {
                Render(spokenEnd);
                return;
            }

            // Spans the separating space as well as the next word, so the gap closes up at the same rate.
            int nextEnd = wordOffsets[wordIndex + 1] + words[wordIndex + 1].Length;
            Render(spokenEnd + Mathf.RoundToInt(Mathf.Clamp01(wordFraction) * (nextEnd - spokenEnd)));
        }

        /// <summary>Reveals the whole line at once, for when playback has finished.</summary>
        public void RevealAll() => Render(plainText.Length);

        /// <summary>
        /// Puts a placeholder in the bubble instead of the line, for the wait between asking for a line and the
        /// first audio arriving. The next call to SetProgress or RevealAll takes the bubble back to the line.
        /// </summary>
        public void SetWaiting(string indicator)
        {
            isWaiting = true;
            lineLabel.text = indicator ?? string.Empty;
        }

        public void Show() => style.display = DisplayStyle.Flex;

        public void Hide() => style.display = DisplayStyle.None;

        private void Render(int characters)
        {
            characters = Mathf.Clamp(characters, 0, plainText.Length);
            // The bubble is showing the waiting indicator rather than the line, so the cached reveal count says
            // nothing about what is on screen and cannot be used to skip the render.
            if (characters == revealedCharacters && !isWaiting) return;
            isWaiting = false;
            revealedCharacters = characters;

            // The unheard remainder is kept in the label, tagged fully transparent, so it still takes up space
            // and the visible part does not jump around as words arrive.
            lineLabel.text = characters >= plainText.Length
                ? plainText
                : plainText.Substring(0, characters) + "<alpha=#00>" + plainText.Substring(characters);
        }

        /// <summary>
        /// Splits a segment's text into the words the reveal is driven by. Whitespace-separated, empties dropped,
        /// which is the same split the sample uses when it inserts the sample request markers - the two have to
        /// agree or the markers would not line up with the words.
        /// </summary>
        public static string[] SplitWords(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
