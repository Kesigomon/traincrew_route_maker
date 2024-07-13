using System.Text;

namespace Traincrew_test;
using TrainCrew;

class Program
{
    private static async Task Main(string[] args)
    {
        Program p = new Program();
        await p.main();
    }

    async Task main()
    {
        Directory.CreateDirectory("data");
        TrainCrewInput.Init();
        var first = true;
        var previousGameScreen = TrainCrewInput.gameState.gameScreen;
        var previousDiaName = "";
        var stations = new HashSet<string>();
        FileStream? fs = null;
        while (true)
        {
            var state = TrainCrewInput.GetTrainState();
            var timer = Task.Delay(15);
            
            var gameScreen = TrainCrewInput.gameState.gameScreen;
            if (first || previousGameScreen is not (GameScreen.MainGame or GameScreen.MainGame_Pause)
                && gameScreen == GameScreen.MainGame)
            {
                TrainCrewInput.RequestData(DataRequest.Signal);
                stations.Clear();
                first = false;
            }
            if(gameScreen is not (GameScreen.MainGame or GameScreen.MainGame_Pause) && fs != null)
            {
                fs.Close();
                fs = null;
            }
            previousGameScreen = gameScreen;
            
            
            if (state.diaName != previousDiaName)
            {
                var filename = $"data/{state.diaName}.csv"; 
                fs = File.Create(filename);
                const string txt = "name,distance,totalLength,position,beaconSpeed,beaconPosition,beaconType\n";
                await fs.WriteAsync(Encoding.UTF8.GetBytes(txt).AsMemory(0, txt.Length));
                previousDiaName = state.diaName;
            }
            foreach (var signal in TrainCrewInput.signals)
            {
                if(fs == null)
                {
                    break;
                }
                var name = signal.name;
                if (stations.Contains(name))
                {
                    continue;
                }
                foreach (
                    var body in from beacon in signal.beacons 
                    let body = $"{name},{signal.distance},{state.TotalLength},{signal.distance + state.TotalLength}," 
                    select body + $"{beacon.speed},{beacon.distance + state.TotalLength},{beacon.type}\n")
                {
                    var bytes = Encoding.UTF8.GetBytes(body);
                    await fs.WriteAsync(bytes.AsMemory(0, bytes.Length));
                }
                stations.Add(name);
            }
            if(fs != null)
            {
                await fs.FlushAsync();
            }
            await timer;

        }
        TrainCrewInput.Dispose();
    }
}