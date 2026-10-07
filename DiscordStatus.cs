using System.Collections.Generic;

namespace HwinfoSensorPanel;

public sealed class DiscordStatus
{
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "offline";
    public string Activity { get; set; } = string.Empty;
    public string VoiceChannel { get; set; } = string.Empty;
    public string Guild { get; set; } = string.Empty;
    public List<DiscordParticipant> Participants { get; set; } = new();
}

public sealed class DiscordParticipant
{
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "offline";
    public string Activity { get; set; } = string.Empty;
    public string VoiceChannel { get; set; } = string.Empty;
    public bool Muted { get; set; }
    public bool Deafened { get; set; }
}
