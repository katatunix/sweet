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

type Device =
    { ProfileIndex: int
      DeviceIndex: int }
    static member create a b = { ProfileIndex = a; DeviceIndex = b }

type State = Map<Device, int>

module State =
    let init profileNumber (deviceNumber: int -> int) : State =
        seq {
            for a = 0 to profileNumber - 1 do
                for b = 0 to (deviceNumber a) - 1 do
                    yield Device.create a b, 0
        }
        |> Map.ofSeq

    let private random = Random()

    let private minByRandomly projection xs =
        let u = xs |> List.minBy projection |> projection
        let xs = xs |> List.filter (projection >> (=)u)
        xs[random.Next xs.Length]

    let next (state: State) : State * Device =
        let device, count =
            state
            |> Map.toList
            |> minByRandomly snd
        state |> Map.add device (count+1), device

    let nextMulti number state =
        ((state, []), seq { 1..number })
        ||> Seq.fold (fun (state, devices) _ ->
            let state, device = next state
            state, device :: devices
        )

module Miner =
    type StartResult =
        { ProcessId: int
          FileName: string
          Args: string }

    let start (config: Config) (state: State) =
        let state, devices = state |> State.nextMulti config.RunDevicesNumber
        let results =
            devices
            |> List.groupBy (fun d -> d.ProfileIndex)
            |> List.map (fun (profileIndex, devices) ->
                let profile = config.Profiles.[profileIndex]
                let fileName = profile.MinerFileName |> Path.GetFullPath
                let args =
                    [   yield profile.MinerParams

                        yield profile.UsedDevicesParamName
                        yield devices
                              |> Seq.map (fun d -> string d.DeviceIndex)
                              |> String.concat profile.Separator

                        for p in profile.DeviceSpecificParams do
                            yield p.Key
                            yield devices
                                  |> Seq.map (fun d -> p.Value.[d.DeviceIndex])
                                  |> String.concat profile.Separator
                    ]
                    |> String.concat " "
                use proc = Process.Start (fileName, args)
                { ProcessId = proc.Id
                  FileName = proc.StartInfo.FileName
                  Args = proc.StartInfo.Arguments }
            )
        state, results

let handle (config: Config) (state: State) =
    printfn "\n================================= %A =================================" DateTime.Now
    printfn "Config:\n%A" config
    printfn "State:\n%A" state

    let state, results = Miner.start config state

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

    state

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
    let version = "v1.12"
    printfn "sweet %s - nghia.buivan@hotmail.com" version

    let config = Config.load "sweet.cfg"

    use timer = createUptimeTimer (sprintf "sweet %s" version) DateTime.Now
    timer.Start ()

    let rec loop state = state |> handle config |> loop

    State.init config.Profiles.Length (fun i -> config.Profiles.[i].TotalDevicesNumber)
    |> loop
    |> ignore

    0
