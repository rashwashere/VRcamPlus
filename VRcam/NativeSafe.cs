using GTA;
using GTA.Math;
using GTA.Native;

namespace VRCam
{
    /// <summary>
    /// Native-only replacements for ScriptHookVDotNet helpers that read game
    /// memory. On GTA V Enhanced the ScriptHookVDotNet 3.7 nightly's entity-address
    /// pattern is wrong, so Ped.Exists()/Vehicle.Exists() (they compare
    /// EntityType, read from memory), Entity.ForwardVector, Ped.CurrentVehicle
    /// and friends crash the game with an AccessViolation. Everything here
    /// goes through natives, which work on both games.
    /// </summary>
    public static class Safe
    {
        public static bool Exists(Entity e)
        {
            return e != null && e.Handle != 0 && Function.Call<bool>(Hash.DOES_ENTITY_EXIST, e.Handle);
        }

        public static bool Exists(int handle)
        {
            return handle != 0 && Function.Call<bool>(Hash.DOES_ENTITY_EXIST, handle);
        }

        public static Vector3 Forward(Entity e)
        {
            return Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, e.Handle);
        }

        public static Vector3 Forward(int handle)
        {
            return Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, handle);
        }

        public static Vector3 Position(int handle)
        {
            return Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, handle, false);
        }

        public static float Heading(int handle)
        {
            return Function.Call<float>(Hash.GET_ENTITY_HEADING, handle);
        }

        public static Vector3 Velocity(int handle)
        {
            return Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, handle);
        }

        // World.CreatePed/CreateProp/CreateVehicle check the model through
        // NativeMemory (Model.IsPed) - null/crash on Enhanced. Spawn with the
        // natives and wrap the handle with SHVDN's internal constructors.
        private static T Wrap<T>(int handle) where T : Entity
        {
            if (handle == 0) return null;
            return (T)System.Activator.CreateInstance(typeof(T),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, new object[] { handle }, null);
        }

        private static bool Load(Model model)
        {
            if (!model.IsValid || !model.IsInCdImage) return false;
            model.Request();
            for (int i = 0; i < 50 && !model.IsLoaded; i++) Script.Wait(100);
            return model.IsLoaded;
        }

        public static Ped CreatePed(Model model, Vector3 pos, float heading = 0f)
        {
            if (!Load(model)) return null;
            return Wrap<Ped>(Function.Call<int>(Hash.CREATE_PED, 26, model.Hash, pos.X, pos.Y, pos.Z, heading, false, false));
        }

        public static Prop CreateProp(Model model, Vector3 pos, bool dynamic)
        {
            if (!Load(model)) return null;
            return Wrap<Prop>(Function.Call<int>(Hash.CREATE_OBJECT, model.Hash, pos.X, pos.Y, pos.Z, false, false, dynamic));
        }

        public static Vehicle CreateVehicle(Model model, Vector3 pos, float heading)
        {
            if (!Load(model)) return null;
            return Wrap<Vehicle>(Function.Call<int>(Hash.CREATE_VEHICLE, model.Hash, pos.X, pos.Y, pos.Z, heading, false, false, false));
        }

        /// <summary>Handle of the vehicle the ped is sitting in, or 0.</summary>
        public static int CurrentVehicle(Ped ped)
        {
            if (!Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, ped.Handle, false))
                return 0;
            return Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, ped.Handle, false);
        }
    }
}
