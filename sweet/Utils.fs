module Sweet.Utils

open System
open System.Threading
open System.Diagnostics

let stopProcess (id: int) =
    let mutable still = true
    while still do
        use proc = Process.Start ("stop.exe", string id)
        proc.WaitForExit ()
        match proc.ExitCode with
        | 0  -> still <- false
        | -1 -> failwith "bug"
        | -2 -> still <- false
        | -3 -> Thread.Sleep 3000
        | _  -> failwith "bug"

let countdown (length: TimeSpan) (intervalMs: int) tick =
    let exp = DateTime.Now + length
    let mutable over = false
    while not over do
        let now = DateTime.Now
        if now < exp then
            tick (exp - now)
            Thread.Sleep intervalMs
        else
            over <- true
