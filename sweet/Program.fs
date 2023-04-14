module Sweet.Main

open System
open System.Timers
open System.IO
open System.Diagnostics
open FSharp.Json

type Profile =
    { MinerFileName: string // gminer.exe
      MinerParams: string
      TotalDevicesNumber: int // 8
      DeviceSpecificParams: Map<string, string list>
      UsedDevicesParamName: string // -d
      Separator: string // space or comma
    }

type Config =
    { Profiles: Profile list
      RunDevicesNumber: int // 2
      IntervalHours: float // 6.0
      MinerStopSeconds: int // 10
    }
    static member load path =
        path |> File.ReadAllText |> Json.deserialize : Config

type State =
    { CurrentDeviceIndex: int }

    static member Default =
        { CurrentDeviceIndex = 0 }

    static member clamp (config: Config) (state: State) =
        let totalDevicesNumber = config.Profiles |> List.sumBy (fun p -> p.TotalDevicesNumber)
        { state with
            CurrentDeviceIndex = state.CurrentDeviceIndex % totalDevicesNumber }

    static member next (config: Config) (state: State) =
        { state with CurrentDeviceIndex = state.CurrentDeviceIndex + config.RunDevicesNumber }
        |> State.clamp config

    static member load path =
        try path |> File.ReadAllText |> Json.deserialize
        with _ -> State.Default

    static member save path (state: State) =
        let json = state |> Json.serialize
        File.WriteAllText (path, json)

module Miner =
    let private genDevicesToRun (config: Config) (state: State) =
        Seq.initInfinite (fun _ ->
            seq {
                for profileIndex = 0 to config.Profiles.Length - 1 do
                    for deviceIndex = 0 to config.Profiles.[profileIndex].TotalDevicesNumber - 1 do
                        yield profileIndex, deviceIndex
            }
        )
        |> Seq.concat
        |> Seq.skip state.CurrentDeviceIndex
        |> Seq.take config.RunDevicesNumber
        |> List.ofSeq
        |> List.groupBy fst
        |> List.map (fun (profileIndex, devices) -> profileIndex, devices |> List.map snd)
        |> Map.ofList

    type StartResult =
        { ProcessId: int
          FileName: string
          Args: string }

    let start (config: Config) (state: State) =
        genDevicesToRun config state
        |> Map.toList
        |> List.map (fun (profileIndex, deviceIndices) ->
            let profile = config.Profiles.[profileIndex]
            let fileName = profile.MinerFileName |> Path.GetFullPath
            let args =
                [   yield profile.MinerParams

                    yield profile.UsedDevicesParamName
                    yield deviceIndices |> List.map string |> String.concat profile.Separator

                    for p in profile.DeviceSpecificParams do
                        yield p.Key
                        yield deviceIndices |> List.map (fun i -> p.Value.[i]) |> String.concat profile.Separator
                ]
                |> String.concat " "
            use proc = Process.Start (fileName, args)
            { ProcessId = proc.Id
              FileName = proc.StartInfo.FileName
              Args = proc.StartInfo.Arguments }
        )

let handle (config: Config) (state: State) =
    printfn "\n================================= %A =================================" DateTime.Now
    printfn "Config:\n%A" config
    printfn "State:\n%A" state

    let results = Miner.start config state
    printfn "%d miner(s) started" results.Length
    results
    |> List.iteri (fun i result ->
        printfn "Miner #%d:" i
        printfn ">>Command: %s %s" result.FileName result.Args
        printfn ">>Process ID: %d" result.ProcessId
    )

    let fmt (ts: TimeSpan) = ts.ToString @"hh\:mm\:ss"

    let interval = TimeSpan.FromHours config.IntervalHours
    printfn "Miner(s) will be stopped at %A" (DateTime.Now + interval)
    Utils.countdown interval 1000 (fun ts -> Console.Write ("\rNow waiting for {0}", fmt ts))
    Console.WriteLine ()

    results
    |> List.iteri (fun i result ->
        printfn "Stop miner #%d with process ID %d" i result.ProcessId
        Utils.stopProcess result.ProcessId
    )

    Utils.countdown
        (TimeSpan.FromSeconds (float config.MinerStopSeconds))
        1000
        (fun ts -> Console.Write ("\rWaiting for miner(s) to be stopped completely {0}", fmt ts))
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
    let version = "v1.11"
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
