﻿//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using HarmonyLib;
//using NebulaModel.Logger;
//using NebulaModel.Packets.Belt;
//using NebulaWorld;
//using NebulaWorld.Factory;
using System.IO;
using UnityEngine;
using System.Text;
using System;
using System.Collections.Generic;

namespace NebulaPatcher.Patches.Dynamic
{
    [HarmonyPatch(typeof(CargoPath))]
    class CargoPath_Patch
    {

        [HarmonyPrefix]
        [HarmonyPatch("Export")]
        public static bool Export_Prefix(
            ref CargoPath __instance, 
            ref int ___capacity, 
            ref int ___bufferLength, 
            ref int ___chunkCapacity, 
            ref int ___chunkCount, 
            ref int ___updateLen, 
            BinaryWriter w
        )
        {

            //// The original implementation
            //w.Write(0);
            //w.Write(__instance.id);
            //w.Write(___capacity);
            //w.Write(___bufferLength);
            //w.Write(___chunkCapacity);
            //w.Write(___chunkCount);
            //w.Write(___updateLen);
            //w.Write(__instance.closed);
            //w.Write((__instance.outputPath == null) ? 0 : __instance.outputPath.id);
            //w.Write((__instance.outputPath == null) ? -1 : __instance.outputIndex);
            //w.Write(__instance.belts.Count);
            //w.Write(__instance.inputPaths.Count);
            //w.Write(__instance.buffer, 0, ___bufferLength);
            //for (int i = 0; i < ___chunkCount; i++)
            //{
            //    w.Write(__instance.chunks[i * 3]);
            //    w.Write(__instance.chunks[i * 3 + 1]);
            //    w.Write(__instance.chunks[i * 3 + 2]);
            //}
            //for (int j = 0; j < ___bufferLength; j++)
            //{
            //    w.Write(__instance.pointPos[j].x);
            //    w.Write(__instance.pointPos[j].y);
            //    w.Write(__instance.pointPos[j].z);
            //    w.Write(__instance.pointRot[j].x);
            //    w.Write(__instance.pointRot[j].y);
            //    w.Write(__instance.pointRot[j].z);
            //    w.Write(__instance.pointRot[j].w);
            //}
            //for (int k = 0; k < __instance.belts.Count; k++)
            //{
            //    w.Write(__instance.belts[k]);
            //}
            //for (int l = 0; l < __instance.inputPaths.Count; l++)
            //{
            //    w.Write(__instance.inputPaths[l]);
            //}

            var surfaceRelativeRotations = new Quaternion[___bufferLength];

            //w.Write(0);
            // Set a special int that we will use to indentify wheter whe should process this normally or use custom optimized method
            // NOTE: This needs to be 4 chars long, else it wont work
            w.Write(BitConverter.ToInt32(Encoding.ASCII.GetBytes("nb00"), 0));
            //w.Write(1337);

            w.Write(__instance.id);
            w.Write(___capacity);
            w.Write(___bufferLength);
            w.Write(___chunkCapacity);
            w.Write(___chunkCount);
            w.Write(___updateLen);
            w.Write(__instance.closed);
            w.Write((__instance.outputPath == null) ? 0 : __instance.outputPath.id);
            w.Write((__instance.outputPath == null) ? -1 : __instance.outputIndex);
            w.Write(__instance.belts.Count);
            w.Write(__instance.inputPaths.Count);
            w.Write(__instance.buffer, 0, ___bufferLength);
            for (int i = 0; i < ___chunkCount; i++)
            {
                w.Write(__instance.chunks[i * 3]);
                w.Write(__instance.chunks[i * 3 + 1]);
                w.Write(__instance.chunks[i * 3 + 2]);
            }
            for (int j = 0; j < ___bufferLength; j++)
            {
                w.Write(__instance.pointPos[j].x);
                w.Write(__instance.pointPos[j].y);
                w.Write(__instance.pointPos[j].z);
                //w.Write(__instance.pointRot[j].x);
                //w.Write(__instance.pointRot[j].y);
                //w.Write(__instance.pointRot[j].z);
                //w.Write(__instance.pointRot[j].w);

                var originalPosition = __instance.pointPos[j];
                var originalRotation = __instance.pointRot[j];
                //var surfaceRelativeRotation = originalRotation;
                //surfaceRelativeRotation.SetLookRotation(Vector3.up, originalPosition);
                var surfaceRelativeRotation = Quaternion.Inverse(Quaternion.LookRotation(originalPosition, Vector3.up)) * originalRotation;
                surfaceRelativeRotations[j] = surfaceRelativeRotation;

                //Debug.Log("bufferLength: " + ___bufferLength);
                //Debug.Log(string.Concat(new object[]
                //{
                //"Exported position: x ",
                //originalPosition.x,
                //" y ",
                //originalPosition.y,
                //" z ",
                //originalPosition.z
                //}));
                //Debug.Log(string.Concat(new object[]
                //{
                //"Exported rotation: x ",
                //originalRotation.x,
                //" y ",
                //originalRotation.y,
                //" z ",
                //originalRotation.z,
                //" w ",
                //originalRotation.w
                //}));

                //Debug.Log(string.Concat(new object[]
                //{
                //"Relative to surface: ",
                //originalRotation.x,
                //originalRotation.y,
                //originalRotation.z,
                //originalRotation.w
                //}));

                //Debug.Log($"Original rotation: x {originalRotation.x} y {originalRotation.y} z {originalRotation.z} w {originalRotation.w}");
                //Debug.Log($"Rotation relative to surface: x {surfaceRelativeRotation.x} y {surfaceRelativeRotation.y} z {surfaceRelativeRotation.z} w {surfaceRelativeRotation.w}");

                // Code that adds the simmilairity flag
                var hasRelativelySimmilairRotation = false;
                if (j > 0)
                {
                    var angularDiff = Quaternion.Angle(surfaceRelativeRotations[j], surfaceRelativeRotations[j - 1]);
                    Debug.Log($"Angular difference between {j} and {j - 1}: {angularDiff}");
                    if (angularDiff == 0)
                    {
                        //Debug.Log($"This is really 0!");
                        hasRelativelySimmilairRotation = true;
                    }
                }
                w.Write(hasRelativelySimmilairRotation);

                if (!hasRelativelySimmilairRotation)
                {
                    w.Write(__instance.pointRot[j].x);
                    w.Write(__instance.pointRot[j].y);
                    w.Write(__instance.pointRot[j].z);
                    w.Write(__instance.pointRot[j].w);
                }

                //Gizmos.color = Color.red;
                //Gizmos.DrawSphere(originalPosition, 1000);

            }
            for (int k = 0; k < __instance.belts.Count; k++)
            {
                w.Write(__instance.belts[k]);
            }
            for (int l = 0; l < __instance.inputPaths.Count; l++)
            {
                w.Write(__instance.inputPaths[l]);
            }

            // Skip the original function
            return false;

        }

        [HarmonyPrefix]
        [HarmonyPatch("Import")]
        public static bool Import_Prefix(
            ref CargoPath __instance,
            //ref int ___capacity,
            ref int ___bufferLength,
            //ref int ___chunkCapacity,
            ref int ___chunkCount,
            ref int ___updateLen,
            BinaryReader r
        )
        {
            __instance.Free();


            //r.ReadInt32();
            var version = Encoding.ASCII.GetString(r.ReadBytes(4));
            //var version = r.ReadInt32();

            switch (version)
            {
                case "nb00":
                    {
                        Debug.Log($"!!!!!!!!!Importing CargoPath data with nb00 format!!!!!!!!!!");

                        __instance.id = r.ReadInt32();
                        __instance.SetCapacity(r.ReadInt32());
                        ___bufferLength = r.ReadInt32();
                        __instance.SetChunkCapacity(r.ReadInt32());
                        ___chunkCount = r.ReadInt32();
                        ___updateLen = r.ReadInt32();
                        __instance.closed = r.ReadBoolean();
                        __instance.outputPathIdForImport = r.ReadInt32();
                        __instance.outputIndex = r.ReadInt32();
                        int beltsCount = r.ReadInt32();
                        int inputPathsCount = r.ReadInt32();
                        r.BaseStream.Read(__instance.buffer, 0, ___bufferLength);
                        for (int i = 0; i < ___chunkCount; i++)
                        {
                            __instance.chunks[i * 3] = r.ReadInt32();
                            __instance.chunks[i * 3 + 1] = r.ReadInt32();
                            __instance.chunks[i * 3 + 2] = r.ReadInt32();
                        }
                        for (int j = 0; j < ___bufferLength; j++)
                        {
                            __instance.pointPos[j].x = r.ReadSingle();
                            __instance.pointPos[j].y = r.ReadSingle();
                            __instance.pointPos[j].z = r.ReadSingle();
                            
                            var sameRelativeRotationAsPrevious = r.ReadBoolean();
                            if (sameRelativeRotationAsPrevious)
                            {
                                Debug.Log($"Part {j} has same relative rotation as part {j - 1}");

                                var originalPosition = __instance.pointPos[j];

                                var previousPosition = __instance.pointPos[j-1];
                                var previousRotation = __instance.pointRot[j-1];

                                // TODO: This needs to be based on the first previos rotation in the chain to prevent compounding presision loss
                                var previousSurfaceRelativeRotation = Quaternion.Inverse(Quaternion.LookRotation(previousPosition, Vector3.up)) * previousRotation;

                                //var calculatedRotation = Quaternion.Inverse(previousSurfaceRelativeRotation) * Quaternion.LookRotation(originalPosition, Vector3.up);
                                //var calculatedRotation = Quaternion.LookRotation(originalPosition, Vector3.up) * Quaternion.Inverse(previousSurfaceRelativeRotation);
                                //var calculatedRotation = Quaternion.Inverse(Quaternion.LookRotation(originalPosition, Vector3.up)) * previousSurfaceRelativeRotation;
                                //var calculatedRotation = previousSurfaceRelativeRotation * Quaternion.Inverse(Quaternion.LookRotation(originalPosition, Vector3.up));
                                //var calculatedRotation = Quaternion.Inverse(Quaternion.LookRotation(originalPosition, Vector3.up)) * Quaternion.Inverse(previousSurfaceRelativeRotation);
                                //var calculatedRotation =  Quaternion.Inverse(previousSurfaceRelativeRotation) * Quaternion.Inverse(Quaternion.LookRotation(originalPosition, Vector3.up));
                                //var calculatedRotation = previousSurfaceRelativeRotation * Quaternion.LookRotation(originalPosition, Vector3.up);
                                var calculatedRotation =  Quaternion.LookRotation(originalPosition, Vector3.up) * previousSurfaceRelativeRotation;

                                Debug.Log($"Calculated rotation: x {calculatedRotation.x} y {calculatedRotation.y} z {calculatedRotation.z} w {calculatedRotation.w}");

                                //__instance.pointRot[j].x = r.ReadSingle();
                                //__instance.pointRot[j].y = r.ReadSingle();
                                //__instance.pointRot[j].z = r.ReadSingle();
                                //__instance.pointRot[j].w = r.ReadSingle();

                                __instance.pointRot[j] = calculatedRotation;

                                //// Temporary to get keep the reader moving
                                //r.ReadSingle();
                                //r.ReadSingle();
                                //r.ReadSingle();
                                //r.ReadSingle();

                                Debug.Log($"Original rotation: x {__instance.pointRot[j].x} y {__instance.pointRot[j].y} z {__instance.pointRot[j].z} w {__instance.pointRot[j].w}");

                                var angularDiff = Quaternion.Angle(calculatedRotation, __instance.pointRot[j]);
                                Debug.Log($"Angular difference between calculated and original rotation: {angularDiff}");

                            } else
                            {
                                __instance.pointRot[j].x = r.ReadSingle();
                                __instance.pointRot[j].y = r.ReadSingle();
                                __instance.pointRot[j].z = r.ReadSingle();
                                __instance.pointRot[j].w = r.ReadSingle();
                            }

                        }
                        __instance.belts = new List<int>();
                        for (int k = 0; k < beltsCount; k++)
                        {
                            __instance.belts.Add(r.ReadInt32());
                        }
                        __instance.inputPaths = new List<int>();
                        for (int l = 0; l < inputPathsCount; l++)
                        {
                            __instance.inputPaths.Add(r.ReadInt32());
                        }

                    }

                    break;
                default:
                    {
                        // The original implementation
                        __instance.id = r.ReadInt32();
                        __instance.SetCapacity(r.ReadInt32());
                        ___bufferLength = r.ReadInt32();
                        __instance.SetChunkCapacity(r.ReadInt32());
                        ___chunkCount = r.ReadInt32();
                        ___updateLen = r.ReadInt32();
                        __instance.closed = r.ReadBoolean();
                        __instance.outputPathIdForImport = r.ReadInt32();
                        __instance.outputIndex = r.ReadInt32();
                        int num = r.ReadInt32();
                        int num2 = r.ReadInt32();
                        r.BaseStream.Read(__instance.buffer, 0, ___bufferLength);
                        for (int i = 0; i < ___chunkCount; i++)
                        {
                            __instance.chunks[i * 3] = r.ReadInt32();
                            __instance.chunks[i * 3 + 1] = r.ReadInt32();
                            __instance.chunks[i * 3 + 2] = r.ReadInt32();
                        }
                        for (int j = 0; j < ___bufferLength; j++)
                        {
                            __instance.pointPos[j].x = r.ReadSingle();
                            __instance.pointPos[j].y = r.ReadSingle();
                            __instance.pointPos[j].z = r.ReadSingle();
                            __instance.pointRot[j].x = r.ReadSingle();
                            __instance.pointRot[j].y = r.ReadSingle();
                            __instance.pointRot[j].z = r.ReadSingle();
                            __instance.pointRot[j].w = r.ReadSingle();
                        }
                        __instance.belts = new List<int>();
                        for (int k = 0; k < num; k++)
                        {
                            __instance.belts.Add(r.ReadInt32());
                        }
                        __instance.inputPaths = new List<int>();
                        for (int l = 0; l < num2; l++)
                        {
                            __instance.inputPaths.Add(r.ReadInt32());
                        }
                    }

                    break;
            }
            

            // Skip the original function
            return false;
        }
    }
}