module Sweet.Main

open System
open System.Timers
open System.IO
open System.Diagnostics
open FSharp.Json

type Config =
    { MinerFileName: string
      MinerParams: string
      DeviceParams: string list option
      UsedDevicesParamName: string
      DeviceNumber: int
      IntervalHours: float
      MinerStopSeconds: int }

    static member load path : Config =
        path |> File.ReadAllText |> Json.deserialize

type State =
    { CurrentDeviceIndex: int }

    static member Default =
        { CurrentDeviceIndex = 0 }

    static member clamp max state =
        { state with CurrentDeviceIndex = state.CurrentDeviceIndex % max }

    static member incCurrentDeviceIndex max state =
        { state with CurrentDeviceIndex = (state.CurrentDeviceIndex + 1) % max }

    static member load path =
        try path |> File.ReadAllText |> Json.deserialize
        with _ -> State.Default

    static member save path (state: State) =
        let json = state |> Json.serialize
        File.WriteAllText (path, json)

module Miner =
    let start (state: State) (config: Config) =
        let fileName = Path.GetFullPath config.MinerFileName
        let args =
            [   yield config.MinerParams
                yield config.UsedDevicesParamName
                yield state.CurrentDeviceIndex |> string
                let deviceParams = config.DeviceParams |> Option.defaultValue []
                if state.CurrentDeviceIndex < deviceParams.Length then
                    yield deviceParams.[state.CurrentDeviceIndex]
            ]
            |> List.filter (String.IsNullOrWhiteSpace >> not)
            |> String.concat " "
        Process.Start (fileName, args)

    let stop (proc: Process) =
        Utils.stopProcess proc.Id

let handle (state: State) (config: Config) =
    printfn "\n================================= %A =================================" DateTime.Now
    printfn "State:\n%A" state
    printfn "Config:\n%A" config

    use proc = Miner.start state config
    printfn "Miner started!"
    printfn "Process ID: %d" proc.Id
    printfn "Command: %s %s" proc.StartInfo.FileName proc.StartInfo.Arguments

    let fmt (ts: TimeSpan) = ts.ToString @"hh\:mm\:ss"

    let interval = TimeSpan.FromHours config.IntervalHours
    printfn "Miner will be stopped at %A" (DateTime.Now + interval)
    Utils.countdown interval 1000 (fun ts -> Console.Write ("\rNow waiting for {0}", fmt ts))
    Console.WriteLine ()

    printfn "Stop miner..."
    Miner.stop proc

    Utils.countdown
        (TimeSpan.FromSeconds (float config.MinerStopSeconds))
        1000
        (fun ts -> Console.Write ("\rWaiting for miner to be stopped completely {0}", fmt ts))
    Console.WriteLine ()

let createUptimeTimer (prefix: string) (beginTime: DateTime) =
    let timer = new Timer (60. * 1000.)
    timer.AutoReset <- true
    timer.Elapsed.Add (fun _ ->
        let uptime = (DateTime.Now - beginTime).ToString @"dd\d\.hh\:mm"
        Console.Title <- sprintf "%s | %s" prefix uptime
    )
    timer

[<EntryPoint>]
let main _ =
    let version = "1.8"
    printfn "sweet v%s - nghia.buivan@hotmail.com" version

    let configFileName = "sweet.cfg"
    let stateFileName = "sweet.sav"

    let config = Config.load configFileName
    let mutable state = State.load stateFileName |> State.clamp config.DeviceNumber

    use timer = createUptimeTimer (sprintf "sweet v%s" version) DateTime.Now
    timer.Start ()

    while true do
        handle state config
        state <- state |> State.incCurrentDeviceIndex config.DeviceNumber
        state |> State.save stateFileName

    timer.Stop ()

    0
