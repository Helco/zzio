using System;

namespace zzre.game.systems
{
    partial class DialogScript
    {
        private string GetScriptSource(in messages.StartDialog message)
        {
            var dbRow = message.NpcEntity.Get<zzio.db.NpcRow>();
            return message.Cause switch
            {
                DialogCause.Trigger => Fallback(dbRow.TriggerScript, DefaultTriggerScript),
                DialogCause.PlayerWon => Fallback(dbRow.VictoriousScript, DefaultVictoriousScript),
                DialogCause.PlayerCaught => Fallback(dbRow.VictoriousScript, DefaultCaughtScript.Replace("ITEM", message.CatchItemId.ToString())),
                DialogCause.PlayerLost => Fallback(dbRow.DefeatedScript, DefaultDefeatedScript),
                DialogCause.PlayerFled => DefaultFledScript,
                DialogCause.NpcFled => DefaultFledScript,
                _ => throw new NotImplementedException($"Unimplemented dialog cause: {message.Cause}")
            };

            static string Fallback(string? s1, string s2) => string.IsNullOrWhiteSpace(s1) ? s2 : s1;
        }

        private const string DefaultTriggerScript = @"
fight.0.0
exit";

        private const string DefaultVictoriousScript = @"
delay.10
npcWizFormEscapes";

        private const string DefaultCaughtScript = @"
# Do you want to catch this fairy - Yes/no
say.2627D615.1
choice.0.B2153621
choice.1.2F5B3621
waitForUser

# User chose Yes
label.0
removePlayerCards.1.0.ITEM
catchWizform
waitForUser
exit

# User chose No
label.1
npcWizFormEscapes
waitForUser
exit";

        private const string DefaultDefeatedScript = @"
killPlayer
exit";

        private const string DefaultFledScript = @"
npcWizFormEscapes";
    }
}
