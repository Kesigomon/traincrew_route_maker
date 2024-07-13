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
        const string directoryName = "route_data";
        Directory.CreateDirectory(directoryName);
        TrainCrewInput.Init();
        var previousSignalName = "";
        var previousStartMeter = 0f;
        // 処理済み信号機
        var signals = new HashSet<string>();
        // CSVファイルストリーム
        FileStream? fs = null;
        while (true)
        {
            // Traincrewから情報取得
            var state = TrainCrewInput.GetTrainState();
            // 内部処理含め次のループまで最小でも15msは間隔を空ける
            var timer = Task.Delay(15);

            var gameScreen = TrainCrewInput.gameState.gameScreen;
            // 乗務中の場合、ファイルストリームが作成されてなければ(基本的には乗務開始時のみ)
            if (gameScreen is GameScreen.MainGame or GameScreen.MainGame_Pause && fs == null)
            {
                // ファイルを作成
                var filename = $"{directoryName}/{state.diaName}.csv";
                fs = File.Create(filename);
                const string txt = "diaName,signalName,StartMeter,EndMeter\n";
                await WriteString(fs, txt);
                
                // 信号情報をリクエスト
                TrainCrewInput.RequestData(DataRequest.Signal);
                signals.Clear();
                previousSignalName = "";
                previousStartMeter = 0f;
            }

            // 乗務終了後、ファイルを閉じる
            if (gameScreen is not (GameScreen.MainGame or GameScreen.MainGame_Pause) && fs != null)
            {
                var txt = $"{state.diaName},{previousSignalName},{previousStartMeter},";
                await WriteString(fs, txt);
                fs.Close();
                fs = null;
            }

            foreach (var signal in TrainCrewInput.signals)
            {
                if (fs == null)
                {
                    break;
                }

                var name = signal.name;
                // 既に記録済み or 停止信号は記録しない(停止信号の場合、当該列車の進路のための信号ではない可能性が高い)
                if (signals.Contains(name) || (TrainCrewInput.signals.Count >= 2 && signal.phase == "R"))
                {
                    continue;
                }
                var startMeter = state.TotalLength + signal.distance;
                if (previousSignalName != "")
                {
                    var txt = $"{state.diaName},{previousSignalName},{previousStartMeter},{startMeter}\n";
                    await WriteString(fs, txt);
                }
                previousSignalName = name;
                previousStartMeter = startMeter;
                signals.Add(name);
            }

            if (fs != null)
            {
                await fs.FlushAsync();
            }

            // 15ms待機
            await timer;
        }

        TrainCrewInput.Dispose();
    }
    
    static ValueTask WriteString(FileStream fs, string txt)
    {
        var bytes = Encoding.UTF8.GetBytes(txt);
        return fs.WriteAsync(bytes.AsMemory(0, bytes.Length));
    }
}