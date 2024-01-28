using zzio;
using zzio.db;
using zzio.scn;
using zzio.script;

const string OriginalPath = @"E:\SteamLibrary\steamapps\common\ZanZarah";
const string ModPath = @"C:\dev\zzmod_amy_does_not_give_a_pack\";
const string ModAppliedPath = @"E:\SteamLibrary\steamapps\common\ZanZarah2\";

string NewRafiScript = CompileScript(
@"setCamera.3001
deployNpcAtTrigger.99.1
delay.15
changeWaypoint.2.1
playSound.4
say.F0118FA5.1
playAmyVoice.trg01d
playPlayerAnimation.34.-1
delay.15
setCamera.2000
changeWaypoint.1.3
setCamera.3003
playAnimation.13.-1
say.2FDD5335.0
waitForUser
setTalkLabels.-1.-1.2
talk.CE516EB5
waitForUser
talk.C7616EB5
waitForUser
setCamera.3004
talk.64CF72B5
waitForUser
talk.8D2D72B5
waitForUser
talk.4B0C76B5
waitForUser
talk.5BD876B5
waitForUser
talk.60F15435
waitForUser
setTalkLabels.10.11.1
talk.5F325835
waitForUser
label.10
    setTalkLabels.-1.-1.2
    talk.F8B376B5
    goto.20
label.11
    setTalkLabels.-1.-1.0
    talk.9CC17AB5
    waitForUser
    deployNpcAtTrigger.78.1
    exit
label.20
    waitForUser
    talk.89CB7AB5
    waitForUser
    talk.5B157AB5
    waitForUser
    talk.91E37AB5
    waitForUser
    setCamera.1003
    setTalkLabels.-1.-1.0
    talk.94B96435
    waitForUser
    changeWaypoint.3.4
    givePlayerCards.1.0.59
    changeDatabase.1D7435A4
    exit");

UID endSay = new(0x55555555);
UID endNpc = new(0x44444444);
string endNpcInit = CompileScript(@"setNpcType.4");
string endNpcUpdate = CompileScript(
@"label.0
    ifPlayerIsClose.15500
        startPrelude
    endIf
    idle
    goto.0");
string endNpcTrigger = CompileScript(
@"say.55555555.0
playAmyVoice.kil00c
waitForUser
delay.3
endGame
");

Table ReadTableAt(string path)
{
    using var stream = new FileStream(Path.Combine(OriginalPath, path), FileMode.Open, FileAccess.Read);
    var table = new Table();
    table.Read(stream);
    return table;
}

void WriteTableTo_(Table table, string basePath, string path)
{
    var fullPath = Path.Combine(basePath, path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
    using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
    table.Write(stream);
}

void WriteTableTo(Table table, string path)
{
    WriteTableTo_(table, ModPath, path);
    WriteTableTo_(table, ModAppliedPath, path);
}

Scene ReadSceneAt(string path)
{
    using var stream = new FileStream(Path.Combine(OriginalPath, path), FileMode.Open, FileAccess.Read);
    var scene = new Scene();
    scene.Read(stream);
    return scene;
}

void WriteSceneTo_(Scene scene, string basePath, string path)
{
    var fullPath = Path.Combine(basePath, path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
    using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
    scene.Write(stream);
}

void WriteSceneTo(Scene scene, string path)
{
    WriteSceneTo_(scene, ModPath, path);
    WriteSceneTo_(scene, ModAppliedPath, path);
}


var npcTable = ReadTableAt(@"Data\_fb0x05.fbs");
var dialogTable = ReadTableAt(@"Data\_fb0x06.fbs");

var rafiIsMadRow = dialogTable.rows[new(0x9CC17AB5)];
rafiIsMadRow.cells[0] = new("Okay, then get lost!");

var rafiNpcRow = npcTable.rows[new(0x55373434)];
rafiNpcRow.cells[1] = new(NewRafiScript);

var byebyeRow = new Row()
{
    uid = endSay,
    cells = new[]
    {
        new Cell("Amy lived happily ever after, never met the White Druid or got to know any fairy. Her mother came back in like an hour."),
        new Cell((int)endNpc.raw),
        new Cell("")
    }
};
dialogTable.rows.Add(endSay, byebyeRow);

var npcRow = new Row()
{
    uid = endNpc,
    cells = new[]
    {
        new Cell(new ForeignKey(new(0xB18BDDB1), new(0x0012F81C))),
        new Cell(endNpcTrigger),
        new Cell(endNpcInit),
        new Cell(endNpcUpdate),
        new Cell(""),
        new Cell(""),
        new Cell("end")
    }
};
npcTable.rows.Add(npcRow.uid, npcRow);

WriteTableTo(dialogTable, @"Data\_fb0x06.fbs");
WriteTableTo(npcTable, @"Data\_fb0x05.fbs");

var gardenScene = ReadSceneAt(@"Resources\Worlds\sc_2421.scn");
var elevatorTrg = gardenScene.triggers.First(t => t.idx == 78);
elevatorTrg.colliderType = TriggerColliderType.Sphere;
elevatorTrg.dir = new(1, 0, 0);
 elevatorTrg.pos = new(140, 32, 192);
 elevatorTrg.radius = 5;
 elevatorTrg.type = TriggerType.Elevator;
 elevatorTrg.ii1 = 100;
  elevatorTrg.ii2 = unchecked((uint)(-1));
 elevatorTrg.ii3 = 2802;
 elevatorTrg.ii4 = 2;

var londonScene = ReadSceneAt(@"Resources\Worlds\sc_2800.scn");
var endNpcTrg = londonScene.triggers.First(t => t.idx == 3);
endNpcTrg.type = TriggerType.NpcStartpoint;
endNpcTrg.ii1 = endNpc.raw;
endNpcTrg.ii2 = 0;
endNpcTrg.ii3 = 0;
endNpcTrg.ii4 = 0;

WriteSceneTo(gardenScene, @"Resources\Worlds\sc_2421.scn");
WriteSceneTo(londonScene, @"Resources\Worlds\sc_2802.scn");

string CompileScript(string script)
{
    var instructions = script
        .Split("\n")
        .Where(l => !string.IsNullOrWhiteSpace(l))
        .Select(l => new RawInstruction(l.Trim()))
        .ToArray();
    using var writer = new StringWriter();
    zzsc.CLI.compile(writer, instructions, null);
    return writer.ToString();
}
