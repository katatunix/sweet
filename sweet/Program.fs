module Sweet.Main

open System
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
        Process.Start (config.MinerFileName, args)

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

[<EntryPoint>]
let main _ =
    printfn "sweet 1.6 - nghia.buivan@hotmail.com"

    let configFileName = "sweet.cfg"
    let stateFileName = "sweet.sav"

    let config = Config.load configFileName
    let mutable state = State.load stateFileName |> State.clamp config.DeviceNumber

    while true do
        handle state config
        state <- state |> State.incCurrentDeviceIndex config.DeviceNumber
        state |> State.save stateFileName

    0
