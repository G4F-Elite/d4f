using System;
using Engine.Audio;
using Engine.Core.Handles;
using Engine.Core.Abstractions;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;
using Engine.Net;
using Engine.Physics;
using Engine.Rendering;
using Engine.UI;

namespace Engine.NativeBindings;

public static class NativeFacadeFactory
{
    public static NativeFacadeSet CreateNativeFacadeSet()
        => CreateNativeFacadeSet(DffNativeInteropApi.Instance);

    public static IPlatformFacade CreatePlatformFacade() => new NativePlatformFacade(new NativePlatformApiStub());

    public static ITimingFacade CreateTimingFacade() => new NativeTimingFacade(new NativeTimingApiStub());

    public static IPhysicsFacade CreatePhysicsFacade() => new NativePhysicsFacade(new NativePhysicsApiStub());

    public static IAudioFacade CreateAudioFacade() => new NativeAudioFacade(new NativeAudioApiStub());

    public static IContentRuntimeFacade CreateContentRuntimeFacade() => new NativeContentRuntimeFacade(new NativeContentApiStub());

    public static INetFacade CreateNetFacade() => new NativeNetFacade(new NativeNetApiStub());

    public static IUiFacade CreateUiFacade() => new NativeUiFacade(new NativeUiApiStub());

    public static IRenderingFacade CreateRenderingFacade() => new NativeRenderingFacade(new NativeRenderingApiStub());

    internal static IPlatformFacade CreatePlatformFacade(INativePlatformApi nativeApi) => new NativePlatformFacade(nativeApi);

    internal static ITimingFacade CreateTimingFacade(INativeTimingApi nativeApi) => new NativeTimingFacade(nativeApi);

    internal static IPhysicsFacade CreatePhysicsFacade(INativePhysicsApi nativeApi) => new NativePhysicsFacade(nativeApi);

    internal static IAudioFacade CreateAudioFacade(INativeAudioApi nativeApi) => new NativeAudioFacade(nativeApi);

    internal static IContentRuntimeFacade CreateContentRuntimeFacade(INativeContentApi nativeApi) => new NativeContentRuntimeFacade(nativeApi);

    internal static INetFacade CreateNetFacade(INativeNetApi nativeApi) => new NativeNetFacade(nativeApi);

    internal static IUiFacade CreateUiFacade(INativeUiApi nativeApi) => new NativeUiFacade(nativeApi);

    internal static IRenderingFacade CreateRenderingFacade(INativeRenderingApi nativeApi) => new NativeRenderingFacade(nativeApi);

    internal static NativeFacadeSet CreateNativeFacadeSet(INativeInteropApi interop)
        => new(new NativeRuntime(interop));

    private sealed class NativePlatformFacade : IPlatformFacade
    {
        private readonly INativePlatformApi _nativeApi;

        public NativePlatformFacade(INativePlatformApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public bool PumpEvents() => _nativeApi.PumpEvents();
    }

    private sealed class NativeTimingFacade : ITimingFacade
    {
        private readonly INativeTimingApi _nativeApi;

        public NativeTimingFacade(INativeTimingApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public FrameTiming NextFrameTiming() => _nativeApi.NextFrameTiming();
    }

    private sealed class NativePhysicsFacade : IPhysicsFacade
    {
        private readonly INativePhysicsApi _nativeApi;

        public NativePhysicsFacade(INativePhysicsApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public void SyncToPhysics(World world) => _nativeApi.SyncToPhysics(world);

        public void Step(TimeSpan deltaTime) => _nativeApi.Step(deltaTime);

        public void SyncFromPhysics(World world) => _nativeApi.SyncFromPhysics(world);

        public bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit)
            => _nativeApi.Raycast(query, out hit);

        public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
            => _nativeApi.Sweep(query, out hit);

        public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
            => _nativeApi.Overlap(query, hits);
    }

    private sealed class NativeUiFacade : IUiFacade
    {
        private readonly INativeUiApi _nativeApi;

        public NativeUiFacade(INativeUiApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public void Update(World world, in FrameTiming timing) => _nativeApi.Update(world, timing);
    }

    private sealed class NativeContentRuntimeFacade : IContentRuntimeFacade
    {
        private readonly INativeContentApi _nativeApi;

        public NativeContentRuntimeFacade(INativeContentApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public void MountPak(string pakPath) => _nativeApi.ContentMountPak(pakPath);

        public void MountDirectory(string directoryPath) => _nativeApi.ContentMountDirectory(directoryPath);

        public byte[] ReadFile(string assetPath) => _nativeApi.ContentReadFile(assetPath);
    }

    private sealed class NativeAudioFacade : IAudioFacade
    {
        private readonly INativeAudioApi _nativeApi;
        private readonly Dictionary<ProceduralSoundRecipe, ulong> _soundsByRecipe = new();
        private readonly HashSet<AudioEmitterHandle> _activeEmitters = new();

        public NativeAudioFacade(INativeAudioApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public AudioEmitterHandle Play(ProceduralSoundRecipe recipe, AudioPlayRequest request)
        {
            ArgumentNullException.ThrowIfNull(recipe);
            ArgumentNullException.ThrowIfNull(request);
            _ = recipe.Validate();
            _ = request.Validate();

            if (!_soundsByRecipe.TryGetValue(recipe, out ulong soundHandle))
            {
                byte[] soundBlob = ProceduralSoundBlobBuilder.BuildMonoPcmBlob(
                    recipe,
                    EstimateDurationSeconds(recipe),
                    request.Loop);
                soundHandle = _nativeApi.CreateSoundFromBlob(soundBlob);
                _soundsByRecipe.Add(recipe, soundHandle);
            }

            AudioEmitterParameters? initialEmitter = request.InitialEmitter;
            var playDesc = new EngineNativeAudioPlayDesc
            {
                Volume = request.Volume,
                Pitch = request.Pitch,
                Bus = (byte)MapBus(request.Bus),
                Loop = request.Loop ? (byte)1 : (byte)0,
                IsSpatialized = initialEmitter is null ? (byte)0 : (byte)1,
                Reserved0 = 0,
                Position0 = initialEmitter?.PositionX ?? 0f,
                Position1 = initialEmitter?.PositionY ?? 0f,
                Position2 = initialEmitter?.PositionZ ?? 0f,
                Velocity0 = 0f,
                Velocity1 = 0f,
                Velocity2 = 0f
            };

            ulong emitterId = _nativeApi.PlaySound(soundHandle, in playDesc);
            if (emitterId == 0u)
            {
                throw new InvalidOperationException("Native audio_play returned an invalid emitter id.");
            }

            var emitter = new AudioEmitterHandle(emitterId);
            _activeEmitters.Add(emitter);
            return emitter;
        }

        public void Stop(AudioEmitterHandle emitter)
        {
            EnsureKnownEmitter(emitter);

            var stopParams = new EngineNativeEmitterParams
            {
                Volume = 0f,
                Pitch = 1f,
                Position0 = 0f,
                Position1 = 0f,
                Position2 = 0f,
                Velocity0 = 0f,
                Velocity1 = 0f,
                Velocity2 = 0f,
                Lowpass = 1f,
                ReverbSend = 0f
            };
            _nativeApi.SetAudioEmitterParams(emitter.Value, in stopParams);
            _activeEmitters.Remove(emitter);
        }

        public void SetListener(in ListenerState listener)
        {
            var nativeListener = new EngineNativeListenerDesc
            {
                Position0 = listener.PositionX,
                Position1 = listener.PositionY,
                Position2 = listener.PositionZ,
                Forward0 = 0f,
                Forward1 = 0f,
                Forward2 = -1f,
                Up0 = 0f,
                Up1 = 1f,
                Up2 = 0f
            };
            _nativeApi.SetAudioListener(in nativeListener);
        }

        public void SetEmitterParameters(AudioEmitterHandle emitter, in AudioEmitterParameters parameters)
        {
            EnsureKnownEmitter(emitter);
            AudioEmitterParameters validated = parameters.Validate();

            var nativeParams = new EngineNativeEmitterParams
            {
                Volume = validated.Volume,
                Pitch = validated.Pitch,
                Position0 = validated.PositionX,
                Position1 = validated.PositionY,
                Position2 = validated.PositionZ,
                Velocity0 = 0f,
                Velocity1 = 0f,
                Velocity2 = 0f,
                Lowpass = 1f,
                ReverbSend = 0f
            };
            _nativeApi.SetAudioEmitterParams(emitter.Value, in nativeParams);
        }

        private void EnsureKnownEmitter(AudioEmitterHandle emitter)
        {
            if (!emitter.IsValid)
            {
                throw new ArgumentException("Emitter handle is invalid.", nameof(emitter));
            }

            if (!_activeEmitters.Contains(emitter))
            {
                throw new KeyNotFoundException($"Emitter '{emitter.Value}' is not active.");
            }
        }

        private static EngineNativeAudioBus MapBus(AudioBus bus)
        {
            return bus switch
            {
                AudioBus.Master => EngineNativeAudioBus.Master,
                AudioBus.Music => EngineNativeAudioBus.Music,
                AudioBus.Sfx => EngineNativeAudioBus.Sfx,
                AudioBus.Ambience => EngineNativeAudioBus.Ambience,
                _ => throw new InvalidDataException($"Unsupported audio bus value: {bus}.")
            };
        }

        private static float EstimateDurationSeconds(ProceduralSoundRecipe recipe)
        {
            float envelopeDuration = recipe.Envelope.AttackSeconds +
                                     recipe.Envelope.DecaySeconds +
                                     recipe.Envelope.ReleaseSeconds;
            return MathF.Max(0.25f, envelopeDuration + 0.25f);
        }
    }

    private sealed class NativeNetFacade : INetFacade
    {
        private readonly INativeNetApi _nativeApi;

        public NativeNetFacade(INativeNetApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public IReadOnlyList<NetEvent> Pump()
        {
            IReadOnlyList<NativeNetEventData> nativeEvents = _nativeApi.NetPump();
            if (nativeEvents.Count == 0)
            {
                return Array.Empty<NetEvent>();
            }

            var events = new NetEvent[nativeEvents.Count];
            for (var i = 0; i < events.Length; i++)
            {
                NativeNetEventData native = nativeEvents[i];
                events[i] = new NetEvent(
                    MapEventKind(native.Kind),
                    MapChannel(native.Channel),
                    native.PeerId,
                    native.Payload);
            }

            return events;
        }

        public void Send(uint peerId, NetworkChannel channel, ReadOnlySpan<byte> payload)
        {
            if (!Enum.IsDefined(channel))
            {
                throw new InvalidDataException($"Unsupported network channel value: {channel}.");
            }

            _nativeApi.NetSend(peerId, (byte)channel, payload);
        }

        private static NetEventKind MapEventKind(byte kind)
        {
            return kind switch
            {
                (byte)EngineNativeNetEventKind.Connected => NetEventKind.Connected,
                (byte)EngineNativeNetEventKind.Disconnected => NetEventKind.Disconnected,
                (byte)EngineNativeNetEventKind.Message => NetEventKind.Message,
                _ => throw new InvalidDataException($"Unsupported native net event kind value: {kind}.")
            };
        }

        private static NetworkChannel MapChannel(byte channel)
        {
            return channel switch
            {
                (byte)NetworkChannel.ReliableOrdered => NetworkChannel.ReliableOrdered,
                (byte)NetworkChannel.Unreliable => NetworkChannel.Unreliable,
                _ => throw new InvalidDataException($"Unsupported native network channel value: {channel}.")
            };
        }
    }

    private sealed class NativeRenderingFacade : IRenderingFacade, IAdvancedCaptureRenderingFacade
    {
        private readonly INativeRenderingApi _nativeApi;

        public NativeRenderingFacade(INativeRenderingApi nativeApi)
        {
            _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public FrameArena BeginFrame(int requestedBytes, int alignment)
            => _nativeApi.BeginFrame(requestedBytes, alignment);

        public void Submit(RenderPacket packet) => _nativeApi.Submit(packet);

        public void Present() => _nativeApi.Present();

        public RenderingFrameStats GetLastFrameStats() => _nativeApi.GetLastFrameStats();

        public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
            => _nativeApi.CreateMeshFromBlob(blob);

        public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
            => _nativeApi.CreateMeshFromCpu(positions, indices);

        public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
            => _nativeApi.CreateTextureFromBlob(blob);

        public TextureHandle CreateTextureFromCpu(
            uint width,
            uint height,
            ReadOnlySpan<byte> rgba8,
            uint strideBytes = 0)
            => _nativeApi.CreateTextureFromCpu(width, height, rgba8, strideBytes);

        public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
            => _nativeApi.CreateMaterialFromBlob(blob);

        public void DestroyResource(ulong handle) => _nativeApi.DestroyResource(handle);

        public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
            => _nativeApi.CaptureFrameRgba8(width, height, includeAlpha);

        public bool TryCaptureFrameRgba16Float(uint width, uint height, out byte[] rgba16Float, bool includeAlpha = true)
        {
            rgba16Float = _nativeApi.CaptureFrameRgba16Float(width, height, includeAlpha);
            return true;
        }
    }
}
