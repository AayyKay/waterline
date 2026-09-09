using Waterline.Infrastructure;

var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var audioDirectory = Path.Combine(repository, "public", "audio");
Directory.CreateDirectory(audioDirectory);
File.WriteAllBytes(Path.Combine(audioDirectory, "waterline-log.wav"), WaterlineAudio.CreateLogCue());
File.WriteAllBytes(Path.Combine(audioDirectory, "waterline-reminder.wav"), WaterlineAudio.CreateReminderCue());
