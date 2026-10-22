using System;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public class SpawnInvincibleBmxRider : Script
{
    // The swap runs once per script session.
    private bool riderSpawned = false;

    public SpawnInvincibleBmxRider()
    {
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (VRCam.VRCamMenu.SuppressHotkeys)
        {
            return;
        }
        // VRCam's Start key, or GTAVive's Enter VR key: the swap must happen while the character is still
        // visible, before VR hides it.
        if (VRCam.KeyBinds.StartCamera.Pressed(e) || VRCam.GtaViveArmedFlag.IsEnterVrKey(e))
        {
            // Skip when the player ped is already invisible (already swapped) or the rider was spawned before.
            VRCam.Trace.Event("Start key pressed (BMX rider) enabled=" + VRCam.VRCamModConfiguration.BmxRiderEnabled + " alreadySpawned=" + riderSpawned);
            if (!VRCam.VRCamModConfiguration.BmxRiderEnabled) return;
            if (!Game.Player.Character.IsVisible || riderSpawned)
            {
                return;
            }
            riderSpawned = true;
            SpawnPedOnBMX();
        }
    }

    // Synchronous LOS probe; returns the hit entity handle or 0.
    private static int ProbeEntityBelow(Vector3 from, Vector3 to, Ped ignore)
    {
        int ray = Function.Call<int>(Hash.START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE,
            from.X, from.Y, from.Z, to.X, to.Y, to.Z, -1, ignore.Handle, 7);
        OutputArgument hit = new OutputArgument(), end = new OutputArgument(),
            normal = new OutputArgument(), entity = new OutputArgument();
        Function.Call(Hash.GET_SHAPE_TEST_RESULT, ray, hit, end, normal, entity);
        return hit.GetResult<bool>() ? entity.GetResult<int>() : 0;
    }

    private void SpawnPedOnBMX()
    {
        // Cache the original player ped and position.
        Ped originalPlayerPed = Game.Player.Character;
        Vector3 spawnPos = originalPlayerPed.Position;
        float heading = originalPlayerPed.Heading;

        // --- Detection Logic for Attachment Target ---
        // Raw handle + natives: wrapping it in an Entity object would go through
        // SHVDN's memory reader, which crashes on GTA V Enhanced.
        int attachHandle = 0;
        Vector3 localOffset = Vector3.Zero;
        bool shouldAttach = false;
        string attachMessage = "";

        if (originalPlayerPed.IsInVehicle())
        {
            attachHandle = VRCam.Safe.CurrentVehicle(originalPlayerPed); // Ped.CurrentVehicle reads memory

            Vector3 worldOffset = originalPlayerPed.Position - VRCam.Safe.Position(attachHandle);
            float headingRad = VRCam.Safe.Heading(attachHandle) * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(-headingRad);
            float sin = (float)Math.Sin(-headingRad);
            localOffset = new Vector3(
                worldOffset.X * cos - worldOffset.Y * sin,
                worldOffset.X * sin + worldOffset.Y * cos,
                worldOffset.Z - 1.6f
            );
            attachMessage = "Camera attached to the vehicle.";
            shouldAttach = true;

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, attachHandle, true, true);
        }
        else
        {
            Vector3 rayStart = originalPlayerPed.Position;
            Vector3 rayEnd = rayStart - new Vector3(0f, 0f, 2f);
            // Plain natives instead of World.Raycast: SHVDN's RaycastResult reads
            // entity memory through offsets that are wrong for GTA V Enhanced
            // (AccessViolation in NativeMemory.GetEntityAddress).
            int hitHandle = ProbeEntityBelow(rayStart, rayEnd, originalPlayerPed);
            int hitType = hitHandle != 0 ? Function.Call<int>(Hash.GET_ENTITY_TYPE, hitHandle) : 0;
            VRCam.Trace.Event("BMX probe below player: hit=" + hitHandle + " type=" + hitType);

            if (hitType == 2) // 2 = vehicle (props are never a stable anchor)
            {
                attachHandle = hitHandle;

                Vector3 targetPos = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, attachHandle, false);
                float targetHeading = Function.Call<float>(Hash.GET_ENTITY_HEADING, attachHandle);
                Vector3 worldOffset = originalPlayerPed.Position - targetPos;
                float targetHeadingRad = targetHeading * (float)Math.PI / 180f;
                float cos = (float)Math.Cos(-targetHeadingRad);
                float sin = (float)Math.Sin(-targetHeadingRad);
                localOffset = new Vector3(
                    worldOffset.X * cos - worldOffset.Y * sin,
                    worldOffset.X * sin + worldOffset.Y * cos,
                    worldOffset.Z - 2.2f
                );

                attachMessage = "Camera attached to the vehicle below.";
                shouldAttach = true;

                if (hitType == 2)
                {
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, attachHandle, true, true);
                }

            }
        }
        // --- End of Detection Logic ---

        // Prepare and request the ped model.
        Model pedModel = new Model("a_m_y_business_01");
        if (!pedModel.IsInCdImage || !pedModel.IsValid)
        {
            GTA.UI.Screen.ShowSubtitle("Invalid ped model: a_m_y_business_01");
            return;
        }
        pedModel.Request(5000);
        if (!pedModel.IsLoaded)
        {
            GTA.UI.Screen.ShowSubtitle("Failed to load ped model.");
            return;
        }

        // Spawn the ambient ped.
        Ped ambientPed = VRCam.Safe.CreatePed(pedModel, spawnPos);
        VRCam.Trace.Event("BMX rider ped spawned handle=" + (ambientPed == null ? 0 : ambientPed.Handle) + " attach=" + shouldAttach + " target=" + attachHandle);
        if (ambientPed == null)
        {
            GTA.UI.Screen.ShowSubtitle("Failed to spawn ped.");
            return;
        }
        ambientPed.IsInvincible = true;

        if (shouldAttach)
        {
            Model bmxModel = new Model("bmx");
            if (!bmxModel.IsInCdImage || !bmxModel.IsValid)
            {
                GTA.UI.Screen.ShowSubtitle("Invalid BMX model: bmx");
                return;
            }
            bmxModel.Request(5000);
            if (!bmxModel.IsLoaded)
            {
                GTA.UI.Screen.ShowSubtitle("Failed to load BMX model.");
                return;
            }

            Vehicle bmx = VRCam.Safe.CreateVehicle(bmxModel, spawnPos, heading);
            if (bmx == null)
            {
                GTA.UI.Screen.ShowSubtitle("Failed to create BMX.");
                return;
            }

            ambientPed.SetIntoVehicle(bmx, VehicleSeat.Driver);
            bmx.IsInvincible = true;

            // Attached entities must NOT also be frozen - the two constraints fight
            // every frame and the ped (and the gameplay camera following it) shakes
            Function.Call(Hash.SET_ENTITY_COLLISION, bmx.Handle, false, false);

            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, bmx.Handle, attachHandle, 0,
                localOffset.X, localOffset.Y, localOffset.Z,
                0f, 0f, 0f,
                false, false, false, false, 0, true);
            GTA.UI.Screen.ShowSubtitle(attachMessage);
            Function.Call(Hash.SET_ENTITY_ALPHA, bmx.Handle, 0, false);
        }
        else
        {
            Vector3 forwardPosition = ambientPed.Position + VRCam.Safe.Forward(ambientPed) * -0.5f;
            ambientPed.Position = forwardPosition;
            Script.Wait(1500);
            Function.Call(Hash.FREEZE_ENTITY_POSITION, ambientPed.Handle, true);
            Function.Call(Hash.SET_ENTITY_COLLISION, ambientPed.Handle, false, false);
        }

        // Finally, switch the player control to the ambient ped and ensure original ped remains visible.
        Function.Call(Hash.CHANGE_PLAYER_PED, Game.Player, ambientPed.Handle, true, true);
        VRCam.Trace.Event("CHANGE_PLAYER_PED -> " + ambientPed.Handle + " (original " + originalPlayerPed.Handle + ")");
        Function.Call(Hash.SET_ENTITY_VISIBLE, originalPlayerPed.Handle, true);
        Function.Call(Hash.SET_ENTITY_VISIBLE, ambientPed.Handle, false);
        Function.Call(Hash.SET_ENTITY_ALPHA, originalPlayerPed.Handle, 255, false);

    }
}
