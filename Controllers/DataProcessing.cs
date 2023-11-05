using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;

using Newtonsoft.Json.Linq;

namespace RatingAPI.Controllers
{
    public class DataProcessing
    {
        public static string maps_dir = "D:\\maps";

        public static int preSegmentSize = 12;
        public static int postSegmentSize = 12;
        public static int predictionSize = 8;
        public static int noteSize = 49;
        public static int segmentSize = preSegmentSize + postSegmentSize + predictionSize;

        // Dictionaries for direction to angle and angle to direction
        private static readonly Dictionary<int, int> DirectionToAngle = new Dictionary<int, int>
        {
            {0, 180},
            {1, 0},
            {2, 90},
            {3, 270},
            {4, 135},
            {5, 225},
            {6, 45},
            {7, 315}
        };

        private static readonly Dictionary<int, int> AngleToDirection = new Dictionary<int, int>
        {
            {180, 0},
            {0, 1},
            {90, 2},
            {270, 3},
            {135, 4},
            {225, 5},
            {45, 6},
            {315, 7}
        };

        // Existing methods like PreprocessNote would be here...

        // Method to get the note direction
        public static int GetNoteDirection(int direction, double angle)
        {
            if (direction == 8)
            {
                return 8;
            }

            int noteAngle = (DirectionToAngle[direction] - (int)Math.Round(angle / 45) * 45) % 360;
            return AngleToDirection[noteAngle];
        }

        // Method to get map notes from json
        public static List<Tuple<double, string>> GetMapNotesFromJson(JObject mapJson, double bpmTimeScale)
        {
            List<Tuple<double, string>> mapNotes;
            if (mapJson.ContainsKey("version") && mapJson["version"].ToString().Split('.')[0] == "3")
            {
                mapNotes = mapJson["colorNotes"]
                    .Where(n => (int)n["c"] == 1 || (int)n["c"] == 0 && (int)n["x"] < 1000 && (int)n["x"] >= 0 && (int)n["y"] < 1000 && (int)n["y"] >= 0)
                    .Select(n => Tuple.Create(
                        (double)n["b"] * bpmTimeScale,
                        $"{n["x"]}{n["y"]}{GetNoteDirection((int)n["d"], (double)n["a"])}{(int)n["c"]}"
                    ))
                    .OrderBy(x => x.Item1).ThenBy(x => x.Item2)
                    .ToList();
            } else
            {
                mapNotes = mapJson["_notes"]
                    .Where(n => (int)n["_type"] == 1 || (int)n["_type"] == 0 && (int)n["_lineIndex"] < 1000 && (int)n["_lineIndex"] >= 0 && (int)n["_lineLayer"] < 1000 && (int)n["_lineLayer"] >= 0)
                    .Select(n => Tuple.Create(
                        (double)n["_time"] * bpmTimeScale,
                        $"{n["_lineIndex"]}{n["_lineLayer"]}{n["_cutDirection"]}{n["_type"]}"
                    ))
                    .OrderBy(x => x.Item1).ThenBy(x => x.Item2)
                    .ToList();
            }
            return mapNotes;
        }

        public static List<double> PreprocessNote(double delta, double deltaOther, int[] noteInfo, double njs, double timeScale)
        {
            delta /= timeScale;
            deltaOther /= timeScale;
            njs *= timeScale;

            double deltaLong = Math.Max(0, 2 - delta) / 2;
            double deltaOtherLong = Math.Max(0, 2 - deltaOther) / 2;
            double deltaShort = Math.Max(0, 0.5 - delta) * 2;
            double deltaOtherShort = Math.Max(0, 0.5 - deltaOther) * 2;

            int colNumber = noteInfo[0];
            int rowNumber = noteInfo[1];
            int directionNumber = noteInfo[2];
            int color = noteInfo[3];

            double[] rowCol = new double[4 * 3];
            double[] direction = new double[10];

            double[] rowCol2 = new double[4 * 3];
            double[] direction2 = new double[10];

            rowCol[colNumber * 3 + rowNumber] = 1;
            direction[directionNumber] = 1;

            List<double> response = new List<double>();

            if (color == 0)
            {
                response.AddRange(rowCol);
                response.AddRange(direction);
                response.AddRange(rowCol2);
                response.AddRange(direction2);
                response.Add(deltaShort);
                response.Add(deltaLong);
                response.Add(deltaOtherShort);
                response.Add(deltaOtherLong);
            } else if (color == 1)
            {
                response.AddRange(rowCol2);
                response.AddRange(direction2);
                response.AddRange(rowCol);
                response.AddRange(direction);
                response.Add(deltaOtherShort);
                response.Add(deltaOtherLong);
                response.Add(deltaShort);
                response.Add(deltaLong);
            }

            response.Add(njs / 30);

            return response;
        }

        public static Tuple<List<double[]>, List<double>> PreprocessMapNotes(List<Tuple<double, string>> mapNotes, double njs, double timeScale)
        {
            List<List<double>> notes = new List<List<double>>();
            List<double> noteTimes = new List<double>();

            double prevZeroNoteTime = 0;
            double prevOneNoteTime = 0;

            foreach (var note in mapNotes)
            {
                double noteTime = note.Item1;
                int[] noteInfo = note.Item2.Split().Select(s => int.Parse(s)).ToArray();
                string type = noteInfo[^1].ToString(); // Assuming the last element of noteInfo can be converted to a string

                double deltaToZero = noteTime - prevZeroNoteTime;
                double deltaToOne = noteTime - prevOneNoteTime;

                if (deltaToZero < 0 || deltaToOne < 0)
                {
                    Console.WriteLine($"{deltaToZero} {deltaToOne}");
                }

                if (type == "0")
                {
                    prevZeroNoteTime = noteTime;
                    List<double> noteProcessed = PreprocessNote(deltaToZero, deltaToOne, noteInfo, njs, timeScale);
                    notes.Add(noteProcessed);
                    noteTimes.Add(noteTime);
                }
                if (type == "1")
                {
                    prevOneNoteTime = noteTime;
                    List<double> noteProcessed = PreprocessNote(deltaToOne, deltaToZero, noteInfo, njs, timeScale);
                    notes.Add(noteProcessed);
                    noteTimes.Add(noteTime);
                }
            }

            return new Tuple<List<double[]>, List<double>>(notes.Select(s => s.ToArray()).ToList(), noteTimes);
        }

        public List<List<double[]>> CreateSegments(List<double[]> notes)
        {
            var emptyRes = new List<List<double[]>> { new List<double[]>(), new List<double[]>() };
            if (notes.Count < predictionSize)
            {
                return emptyRes;
            }

            var segments = new List<List<double[]>>();
            for (int i = 0; i <= notes.Count - predictionSize; i++)
            {
                if (i % predictionSize != 0)
                {
                    continue;
                }

                var preSlice = notes.GetRange(Math.Max(0, i - preSegmentSize), Math.Min(preSegmentSize, i));
                var slice = notes.GetRange(i, predictionSize);
                var postSlice = notes.GetRange(i + predictionSize, Math.Min(postSegmentSize, notes.Count - (i + predictionSize)));

                var preSegment = preSlice.Select(note => note.ToArray()).ToList();
                while (preSegment.Count < preSegmentSize)
                {
                    preSegment.Insert(0, new double[noteSize]);
                }

                var segment = slice.Select(note => note.ToArray()).ToList();

                var postSegment = postSlice.Select(note => note.ToArray()).ToList();
                while (postSegment.Count < postSegmentSize)
                {
                    postSegment.Add(new double[noteSize]);
                }

                var finalSegment = new List<double[]>();
                finalSegment.AddRange(preSegment);
                finalSegment.AddRange(segment);
                finalSegment.AddRange(postSegment);
                segments.Add(finalSegment);
            }

            return segments;
        }

        public int GetFreePointsForMap(JObject mapJson)
        {
            if (mapJson.ContainsKey("version") &&
                mapJson.Value<string>("version").Split('.')[0] == "3" &&
                mapJson.ContainsKey("burstSliders"))
            {
                var burstSliders = mapJson.Value<JArray>("burstSliders");
                int segmentCount = 0;
                foreach (var burstSlider in burstSliders)
                {
                    segmentCount += burstSlider.Value<int>("sc");
                }
                return segmentCount * 20 * 8;
            } else
            {
                return 0;
            }
        }

        public JObject V3_3_0_to_V3(JObject V3_0_0mapData)
        {
            var newMapData = (JObject)V3_0_0mapData.DeepClone();

            foreach (var bpmEvent in newMapData.Value<JArray>("bpmEvents"))
            {
                bpmEvent["b"] = bpmEvent.Value<int?>("b") ?? 0;
                bpmEvent["m"] = bpmEvent.Value<int?>("m") ?? 0;
            }

            foreach (var colorNote in newMapData.Value<JArray>("colorNotes"))
            {
                colorNote["b"] = colorNote.Value<int?>("b") ?? 0;
                colorNote["x"] = colorNote.Value<int?>("x") ?? 0;
                colorNote["y"] = colorNote.Value<int?>("y") ?? 0;
                colorNote["a"] = colorNote.Value<int?>("a") ?? 0;
                colorNote["c"] = colorNote.Value<int?>("c") ?? 0;
                colorNote["d"] = colorNote.Value<int?>("d") ?? 0;
            }

            foreach (var bombNote in newMapData.Value<JArray>("bombNotes"))
            {
                bombNote["b"] = bombNote.Value<int?>("b") ?? 0;
                bombNote["x"] = bombNote.Value<int?>("x") ?? 0;
                bombNote["y"] = bombNote.Value<int?>("y") ?? 0;
            }

            foreach (var obstacle in newMapData.Value<JArray>("obstacles"))
            {
                obstacle["b"] = obstacle.Value<int?>("b") ?? 0;
                obstacle["x"] = obstacle.Value<int?>("x") ?? 0;
                obstacle["y"] = obstacle.Value<int?>("y") ?? 0;
                obstacle["d"] = obstacle.Value<int?>("d") ?? 0;
                obstacle["w"] = obstacle.Value<int?>("w") ?? 0;
                obstacle["h"] = obstacle.Value<int?>("h") ?? 0;
            }

            foreach (var burstSlider in newMapData.Value<JArray>("burstSliders"))
            {
                burstSlider["b"] = burstSlider.Value<int?>("b") ?? 0;
                burstSlider["c"] = burstSlider.Value<int?>("c") ?? 0;
                burstSlider["x"] = burstSlider.Value<int?>("x") ?? 0;
                burstSlider["y"] = burstSlider.Value<int?>("y") ?? 0;
                burstSlider["d"] = burstSlider.Value<int?>("d") ?? 0;
                burstSlider["tb"] = burstSlider.Value<int?>("tb") ?? 0;
                burstSlider["tx"] = burstSlider.Value<int?>("tx") ?? 0;
                burstSlider["ty"] = burstSlider.Value<int?>("ty") ?? 0;
                burstSlider["sc"] = burstSlider.Value<int?>("sc") ?? 8;
                burstSlider["s"] = burstSlider.Value<int?>("s") ?? 1;
            }

            return newMapData;
        }

        public (double? njs, List<Tuple<double, string>> mapNotes, string songName, int freePoints) GetMapData(string hash, string characteristic, int difficulty)
        {
            if (characteristic == null)
            {
                characteristic = "Standard";
            }

            var mapInfoFiles = new List<string>();
            mapInfoFiles.AddRange(Directory.GetFiles($"{maps_dir}/{hash}", "Info.dat", SearchOption.TopDirectoryOnly));
            mapInfoFiles.AddRange(Directory.GetFiles($"{maps_dir}/{hash}", "info.dat", SearchOption.TopDirectoryOnly));
            string mapInfoFile = mapInfoFiles.First();
            double? njs = null;
            List<Tuple<double, string>> mapNotes = null;
            string songName = null;
            int freePoints = 0;

            string fileContent = File.ReadAllText(mapInfoFile);
            JObject mapInfo = JObject.Parse(fileContent);
            double bpm = mapInfo.Value<double>("_beatsPerMinute");
            double bpmTimeScale = 60 / bpm;
            songName = mapInfo.Value<string>("_songName");

            foreach (var beatmapSet in mapInfo["_difficultyBeatmapSets"])
            {
                if (beatmapSet["_beatmapCharacteristicName"].ToString().Replace(" ", "") != characteristic)
                {
                    continue;
                }

                foreach (var beatmap in beatmapSet["_difficultyBeatmaps"])
                {
                    if (beatmap.Value<int>("_difficultyRank") == difficulty)
                    {
                        njs = beatmap.Value<double>("_noteJumpMovementSpeed");
                        string mapFileName = beatmap.Value<string>("_beatmapFilename");
                        string mapFilePath = mapInfoFile.Replace("Info.dat", mapFileName).Replace("info.dat", mapFileName);
                        string mapFileContent = File.ReadAllText(mapFilePath);
                        JObject mapJson = JObject.Parse(mapFileContent);
                        if (mapJson.ContainsKey("version") && Version.Parse(mapJson.Value<string>("version")) > Version.Parse("3.2.0"))
                        {
                            mapJson = V3_3_0_to_V3(mapJson);
                        }
                        mapNotes = GetMapNotesFromJson(mapJson, bpmTimeScale);
                        freePoints = GetFreePointsForMap(mapJson);
                    }
                }
            }

            return (njs, mapNotes, songName, freePoints);
        }

        public void DownloadMap(string hash)
        {
            string mapDir = Path.Combine(maps_dir, hash);

            if (Directory.Exists(mapDir))
            {
                return;
            }

            string beatsaverUrl = $"https://beatsaver.com/api/maps/hash/{hash}";
            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetStringAsync(beatsaverUrl).Result;
                dynamic beatsaverData = JsonConvert.DeserializeObject(response);
                string downloadURL = string.Empty;

                foreach (var version in beatsaverData.versions)
                {
                    if (version.hash.ToString().ToLower() == hash.ToLower())
                    {
                        downloadURL = version.downloadURL;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadURL))
                {
                    throw new Exception("Map download URL not found.");
                }

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; BeatSaverDownloader/1.0)");
                    byte[] data = client.DownloadData(downloadURL);

                    using (var zipStream = new MemoryStream(data))
                    using (var zipArchive = new ZipArchive(zipStream))
                    {
                        Directory.CreateDirectory(mapDir);
                        zipArchive.ExtractToDirectory(mapDir);

                        string[] extractedFiles = Directory.GetFiles(mapDir);
                        foreach (string extractedFile in extractedFiles)
                        {
                            if (!extractedFile.EndsWith(".dat"))
                            {
                                try
                                {
                                    File.Delete(extractedFile);
                                } catch
                                {
                                    // Handle exceptions if required or continue
                                }
                            }
                        }
                    }
                }
            }
        }

        public (List<List<double[]>> segments, string songName, List<double> noteTimes, int freePoints) PreprocessMap(string hash, string characteristic, int difficulty, double timeScale)
        {
            DownloadMap(hash);

            var emptyResponse = (new List<List<double[]>>(), "", new List<double>(), 0);
            var (njs, mapNotes, songName, freePoints) = GetMapData(hash, characteristic, difficulty);
            if (!njs.HasValue || mapNotes == null)
            {
                return emptyResponse;
            }

            var (notes, noteTimes) = PreprocessMapNotes(mapNotes, njs.Value, timeScale);
            List<List<double[]>> segments = CreateSegments(notes);
            return (segments, songName, noteTimes, freePoints);
        }
    }
}
