using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Pokemon
{
    public class SongLoader
    {
        public class SongComponent
        {
            public string SongContent = "";
            public List<float> playerScores = new();
        }

        private List<SongComponent> components = new();
        private static Random random = new();

        public SongLoader(string fileContents, bool _) {
            int lineCount = 0;
            int songCount = 0;
            List<string> headerLines = new();
            List<string> contentLines = new();
            bool isHeader = true;
            
            foreach (string line in fileContents.Split('\n'))
            {
                lineCount++;

                if (line.StartsWith("="))
                    isHeader = false; // Switch to reading content lines after the separator
                else if (isHeader)
                    headerLines.Add(line);
                else
                    contentLines.Add(line);
            }

            string header = string.Join("\n", headerLines);

            foreach (var contentLine in contentLines)
            {
                if (string.IsNullOrWhiteSpace(contentLine)) continue;

                SongComponent component = new()
                {
                    SongContent = $"{header}\n{contentLine}" // Combine header with the content line
                };

                components.Add(component);
                songCount++;
            }

            Debug.Log($"Parsed {lineCount} lines and created {songCount} song components.");
        }

        public SongLoader(string filePath)
        {
            try
            {
                Debug.Log("Attempting to load song from: " + filePath);

                // Ensure the file exists before trying to open it
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"File not found: {filePath}");
                    return;
                }

                using (FileStream file = File.OpenRead(filePath))
                using (StreamReader reader = new(file))
                {
                    ParseFile(reader);
                }

                Debug.Log($"Successfully loaded {components.Count} song components from: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading song from {filePath}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ParseFile(StreamReader reader)
        {
            int lineCount = 0;
            int songCount = 0;
            List<string> headerLines = new();
            List<string> contentLines = new();
            bool isHeader = true;

            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineCount++;

                    if (line.StartsWith("="))
                        isHeader = false; // Switch to reading content lines after the separator
                    else if (isHeader)
                        headerLines.Add(line);
                    else
                        contentLines.Add(line);
                }

                string header = string.Join("\n", headerLines);

                foreach (var contentLine in contentLines)
                {
                    if (string.IsNullOrWhiteSpace(contentLine)) continue;

                    SongComponent component = new()
                    {
                        SongContent = $"{header}\n{contentLine}" // Combine header with the content line
                    };

                    components.Add(component);
                    songCount++;
                }

                Debug.Log($"Parsed {lineCount} lines and created {songCount} song components.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while parsing the file: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public SongComponent GetRandomSongComponent()
        {
            try
            {
                // Step 1: Find unplayed songs
                var unplayedSongs = components.Where(c => c.playerScores.Count == 0).ToList();
                if (unplayedSongs.Count > 0)
                {
                    Debug.Log("Selecting an unplayed song.");
                    return unplayedSongs[random.Next(unplayedSongs.Count)];
                }

                // Step 2: Find weakest performance
                float lowestAverageScore = components.Min(c => c.playerScores.Average());
                var weakestSongs = components
                    .Where(c => Math.Abs(c.playerScores.Average() - lowestAverageScore) < 0.1f) // Allow a small margin for "ties"
                    .ToList();

                if (weakestSongs.Count > 0)
                {
                    Debug.Log("Selecting a song with weakest performance.");
                    return weakestSongs[random.Next(weakestSongs.Count)];
                }

                // Step 3: Fallback to random
                Debug.Log("Selecting a random song.");
                return components[random.Next(components.Count)];
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in GetRandomSongComponent: {ex.Message}\n{ex.StackTrace}");
                return null; // Fallback in case of an error
            }
        }

        public static SongLoader CurrentSong = new(@"X:1
M:C
L:1/4
V:1
K:C
=========================
[V:1] !5!E E F G | G F E D |
[V:1] !5!C C D E | E>D D2 |
[V:1] !5!E E F G | G F E D |
[V:1] !5!C C D E | D>C C2 |
[V:1] !5!D D E D | D E/ F/ E C |
[V:1] !5!D E/ F/ E D | C D G2 |
[V:1] !5!E E F G | G F E D |
[V:1] !5!C C D E | D>C C2 |", false);
    }
}
