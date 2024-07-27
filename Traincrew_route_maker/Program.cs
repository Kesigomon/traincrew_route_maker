using System.Text;

namespace Traincrew_route_maker;

using TrainCrew;

class Program
{
    private static async Task Main(string[] args)
    {
        Program p = new Program();
        try
        {

            await p.main();
        }
        catch (Exception e)
        {
            try
            {
                Directory.CreateDirectory("crashlog");
                await File.WriteAllTextAsync($"crashlog/{DateTime.Now:yyyyMMddHHmmss}.txt", e.ToString());
            }
            catch (Exception)
            {
                // クラッシュログの出力に失敗した場合は何もしない
            }
            TrainCrewInput.Dispose();
        }
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
                var filename = $"{directoryName}/{state.diaName}_{state.CarStates.Count}.csv";
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
                // Todo: 終端はこの位置で良い？
                var normalizedSignalName = normalizeSignalName(previousSignalName, state.stationList);
                var txt = $"{state.diaName},{normalizedSignalName},{previousStartMeter},{state.TotalLength + 20}";
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
                if (
                    signals.Contains(name) // 既に記録済み
                    || signal.name.Contains("中継") // 中継信号機
                    || (TrainCrewInput.signals.Count >= 2 && signal.phase == "R") // 停止信号
                )
                {
                    continue;
                }

                var startMeter = state.TotalLength + signal.distance;
                if (previousSignalName != "")
                {
                    var normalizedSignalName = normalizeSignalName(previousSignalName, state.stationList);
                    var txt = $"{state.diaName},{normalizedSignalName},{previousStartMeter},{startMeter}\n";
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
    }
    
    static string normalizeSignalName(string signalName, List<Station> stations)
    {
        if(signalName.StartsWith("館浜下り場内1L"))
        {
            var station = stations.First(s => s.Name == "館浜");
            var trackNumber = station.StopPosName[3]; 
            var signalId = (char)(trackNumber + 'A' - '1');
            return $"館浜下り場内1L{signalId}";
        }
        if(signalName.StartsWith("大道寺上り場内1R"))
        {
            var station = stations.First(s => s.Name == "大道寺");
            var trackNumber = station.StopPosName[4];
            var signalId = (char)(trackNumber + 'A' - '1');
            return $"大道寺上り場内1R{signalId}";
        }

        return signalName;
    }

    static ValueTask WriteString(FileStream fs, string txt)
    {
        var bytes = Encoding.UTF8.GetBytes(txt);
        return fs.WriteAsync(bytes.AsMemory(0, bytes.Length));
    }
}