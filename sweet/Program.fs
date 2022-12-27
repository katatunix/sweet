module Sweet.Main

open System
open System.Timers
open System.IO
open System.Diagnostics
open FSharp.Json

type Profile =
    { MinerFileName: string
      MinerParams: string
      DeviceParams: string list option
      UsedDevicesParamName: string
      DeviceNumber: int }

type Config =
    { Profiles: Profile list
      IntervalHours: float
      MinerStopSeconds: int }

    static member load path : Config =
        path |> File.ReadAllText |> Json.deserialize

type State =
    { CurrentProfileIndex: int
      CurrentDeviceIndex: int }

    static member Default =
        { CurrentProfileIndex = 0
          CurrentDeviceIndex = 0 }

    static member clamp (config: Config) (state: State) =
        let profileIndex = state.CurrentProfileIndex % config.Profiles.Length
        let deviceIndex = state.CurrentDeviceIndex % config.Profiles.[profileIndex].DeviceNumber
        { state with
            CurrentProfileIndex = profileIndex
            CurrentDeviceIndex = deviceIndex }

    static member next (config: Config) (state: State) =
        let nextDeviceIndex = state.CurrentDeviceIndex + 1
        if nextDeviceIndex < config.Profiles.[state.CurrentProfileIndex].DeviceNumber then
            { state with CurrentDeviceIndex = nextDeviceIndex }
        else
            { state with
                CurrentProfileIndex = (state.CurrentProfileIndex + 1) % config.Profiles.Length
                CurrentDeviceIndex = 0 }

    static member load path =
        try path |> File.ReadAllText |> Json.deserialize
        with _ -> State.Default

    static member save path (state: State) =
        let json = state |> Json.serialize
        File.WriteAllText (path, json)

module Miner =
    let start (config: Config) (state: State) =
        let profile = config.Profiles.[state.CurrentProfileIndex]
        let fileName = profile.MinerFileName |> Path.GetFullPath
        let args =
            [   yield profile.MinerParams
                yield profile.UsedDevicesParamName
                yield state.CurrentDeviceIndex |> string
                let deviceParams = profile.DeviceParams |> Option.defaultValue []
                if state.CurrentDeviceIndex < deviceParams.Length then
                    yield deviceParams.[state.CurrentDeviceIndex]
            ]
            |> List.filter (String.IsNullOrWhiteSpace >> not)
            |> String.concat " "
        Process.Start (fileName, args)

    let stop (proc: Process) =
        Utils.stopProcess proc.Id

let handle (config: Config) (state: State) =
    printfn "\n================================= %A =================================" DateTime.Now
    printfn "Config:\n%A" config
    printfn "State:\n%A" state

    use proc = Miner.start config state
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
    let version = "v1.9"
    printfn "sweet %s - nghia.buivan@hotmail.com" version

    let configFileName = "sweet.cfg"
    let stateFileName = "sweet.sav"

    let config = Config.load configFileName
    let mutable state = State.load stateFileName |> State.clamp config

    use timer = createUptimeTimer (sprintf "sweet %s" version) DateTime.Now
    timer.Start ()

    while true do
        handle config state
        state <- state |> State.next config
        state |> State.save stateFileName

    timer.Stop ()

    0
