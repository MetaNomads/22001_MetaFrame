/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using UnityEngine;

// Copied from Oculus.Interaction into this namespace so RenderLines() can
// accept a target Camera — the only change from the original is the optional
// Camera parameter threaded through to DrawMeshInstancedIndirect.
namespace MetaFrame.Interaction.SkeletonInteraction
{
    public class PolylineRenderer
    {
        private Vector4[] _positions = null;
        private bool _positionsNeedUpdate = false;

        private Color[] _colors = null;
        private bool _colorsNeedUpdate = false;

        private Bounds _bounds;

        private Mesh _baseMesh;
        private Material _material;

        private bool _renderSinglePass;
        private int Copies => _renderSinglePass ? 2 : 1;
        private int BufferSize => _maxLineCount * 2 * Copies;

        private ComputeBuffer _positionBuffer;
        private ComputeBuffer _colorBuffer;
        private ComputeBuffer _argsBuffer;
        private uint[] _argsData;

        private int _positionBufferShaderID = Shader.PropertyToID("_PositionBuffer");
        private int _colorBufferShaderID    = Shader.PropertyToID("_ColorBuffer");
        private int _localToWorldShaderID   = Shader.PropertyToID("_LocalToWorld");
        private int _scaleShaderID          = Shader.PropertyToID("_Scale");

        private int _maxLineCount = 1;
        private Matrix4x4 _matrix = Matrix4x4.identity;
        private float _lineScaleFactor = 1.0f;

        public float LineScaleFactor
        {
            get => _lineScaleFactor;
            set => _lineScaleFactor = value;
        }

        public PolylineRenderer(Material material = null, bool renderSinglePass = true)
        {
            _renderSinglePass = renderSinglePass;

            if (material == null)
                material = new Material(Shader.Find("Custom/PolylineUnlit"));

            _material = new Material(material);

            GameObject baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _baseMesh = baseCube.GetComponent<MeshFilter>().sharedMesh;
            GameObject.DestroyImmediate(baseCube);

            _positions = new Vector4[BufferSize];
            _colors    = new Color[BufferSize];

            _positionBuffer = new ComputeBuffer(BufferSize, 16);
            _positionBuffer.SetData(_positions);
            _colorBuffer = new ComputeBuffer(BufferSize, 16);
            _colorBuffer.SetData(_colors);

            _material.SetBuffer(_positionBufferShaderID, _positionBuffer);
            _material.SetBuffer(_colorBufferShaderID,    _colorBuffer);

            _argsData    = new uint[5] { 0, 0, 0, 0, 0 };
            _argsData[0] = (uint)_baseMesh.GetIndexCount(0);
            _argsData[1] = (uint)(_maxLineCount * Copies);

            _argsBuffer = new ComputeBuffer(1, _argsData.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(_argsData);

            _positionsNeedUpdate = true;
            _colorsNeedUpdate    = true;
        }

        public void Cleanup()
        {
            _positionBuffer.Release();
            _colorBuffer.Release();
            _argsBuffer.Release();
            if (Application.isPlaying)
                GameObject.Destroy(_material);
            else
                GameObject.DestroyImmediate(_material);
        }

        public void SetLines(List<Vector4> positions, Color color)
        {
            SetPositions(positions.Count, positions);
            SetDrawCount(positions.Count / 2);
            SetColor(positions.Count, color);
        }

        public void SetLines(List<Vector4> positions, List<Color> colors, int maxCount = -1)
        {
            int count = maxCount < 0 ? positions.Count : maxCount;
            SetPositions(count, positions);
            SetDrawCount(count / 2);
            SetColors(count, colors);
        }

        private void SetPositions(int count, List<Vector4> positions)
        {
            if (count * Copies > _positions.Length)
            {
                _maxLineCount = count / 2;
                _positions = new Vector4[BufferSize];
                _positionBuffer.Release();
                _positionBuffer = new ComputeBuffer(BufferSize, 16);
                _positionBuffer.SetData(_positions);
            }

            _bounds = new Bounds();
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < count; i += 2)
            {
                for (int j = 0; j < 2; j++)
                {
                    Vector4 position = positions[i + j];
                    for (int k = 0; k < Copies; k++)
                        _positions[(i + k) * Copies + j] = position;

                    Vector3 width = position.w * Vector3.one;
                    Vector3 p    = (Vector3)position;
                    Vector3 pmin = p - width / 2f;
                    Vector3 pmax = p + width / 2f;
                    if (i == 0 && j == 0) { min = pmin; max = pmax; }
                    else
                    {
                        min.x = Mathf.Min(pmin.x, min.x);
                        min.y = Mathf.Min(pmin.y, min.y);
                        min.z = Mathf.Min(pmin.z, min.z);
                        max.x = Mathf.Max(pmax.x, max.x);
                        max.y = Mathf.Max(pmax.y, max.y);
                        max.z = Mathf.Max(pmax.z, max.z);
                    }
                }
            }
            _bounds.SetMinMax(min, max);
            _positionsNeedUpdate = true;
        }

        private void SetColors(int count, List<Color> colors)
        {
            PrepareColorBuffer(count);
            for (int i = 0; i < count; i += 2)
                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < Copies; k++)
                        _colors[(i + k) * Copies + j] = colors[i + j];
            _colorsNeedUpdate = true;
        }

        private void SetColor(int count, Color color)
        {
            PrepareColorBuffer(count);
            for (int i = 0; i < count; i += 2)
                for (int j = 0; j < 2; j++)
                    for (int k = 0; k < Copies; k++)
                        _colors[(i + k) * Copies + j] = color;
            _colorsNeedUpdate = true;
        }

        private void SetDrawCount(int c)
        {
            _argsData[1] = (uint)(c * Copies);
            _argsBuffer.SetData(_argsData);
        }

        private void PrepareColorBuffer(int count)
        {
            if (count * Copies <= _colors.Length) return;
            _maxLineCount = count / 2;
            _colors = new Color[BufferSize];
            _colorBuffer.Release();
            _colorBuffer = new ComputeBuffer(BufferSize, 16);
            _colorBuffer.SetData(_colors);
        }

        // ── Only change from original: optional Camera parameter ──────────────
        // Passing a Camera restricts rendering to that camera only.
        // Passing null (default) renders to all cameras, matching original behaviour.
        public void RenderLines(Camera camera = null)
        {
            if (_positionsNeedUpdate)
            {
                _positionBuffer.SetData(_positions);
                _material.SetBuffer(_positionBufferShaderID, _positionBuffer);
                _positionsNeedUpdate = false;
            }

            if (_colorsNeedUpdate)
            {
                _colorBuffer.SetData(_colors);
                _material.SetBuffer(_colorBufferShaderID, _colorBuffer);
                _colorsNeedUpdate = false;
            }

            _material.SetFloat(_scaleShaderID,   _lineScaleFactor);
            _material.SetMatrix(_localToWorldShaderID, _matrix);

            Bounds bounds = new Bounds(
                _matrix.MultiplyPoint(_bounds.center),
                _matrix.MultiplyVector(_bounds.size));

            Graphics.DrawMeshInstancedIndirect(
                _baseMesh, 0, _material, bounds, _argsBuffer,
                0, null, UnityEngine.Rendering.ShadowCastingMode.Off,
                false, 0, camera);   // <── camera passed here
        }

        public void SetTransform(Transform transform)
        {
            _matrix = transform.localToWorldMatrix;
        }
    }
}
