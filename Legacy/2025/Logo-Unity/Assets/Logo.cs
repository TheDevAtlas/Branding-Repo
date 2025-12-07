using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

[ExecuteAlways]
public class Logo : MonoBehaviour
{
    public float radius = 1.5f;

    [Header("Materials")]
    public Material material1;
    public Material material2;
    public Material material3;

    [Header("Settings")]
    public Vector3 ringRotation;
    public float ringDistance = 0.1f;
    public float startThickness = 0.01f;
    public float endThickness = 0.04f;
    [Range(3, 1000)]
    public int segments = 60;
    public int seed = 12345;
    
    [Header("Stream Settings")]
    [Range(1, 10)]
    public int streamsPerRing = 3;
    public float streamSpreadStart = 1.0f;
    public float streamSpreadEnd = 0.05f;

    [Header("Stream Colors")]
    public bool useGradient = false;
    public Gradient startColorGradient;

    [Header("Animation")]
    public float animationDuration = 0.8f;
    public float delayBetweenRings = 0.1f;
    public float flyInDistance = 5f;

    [Header("Bloom Animation")]
    public Volume globalVolume;
    public float startThreshold = 0.9f;
    public float endThreshold = 0.9f;
    public float startIntensity = 0f;
    public float endIntensity = 5f;
    public float startScatter = 0.7f;
    public float endScatter = 0.7f;

    [Header("Text Animation")]
    public TMP_Text[] logoTexts;
    public float textAnimationDuration = 1.5f;
    public float textDelay = 0.5f;
    public float startSpacing = 50f;
    public float endSpacing = 0f;

    private class StreamData
    {
        public LineRenderer lineRenderer;
        public float spreadMultiplier;
        public Color startColor;
        public Color targetColor;
        public MaterialPropertyBlock mpb;
    }

    private List<StreamData> ring1Streams = new List<StreamData>();
    private List<StreamData> ring2Streams = new List<StreamData>();
    private List<StreamData> ring3Streams = new List<StreamData>();
    private Bloom bloom;
    private float timeElapsed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetupLines();
        SetupBloom();
        timeElapsed = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        if (!Application.isPlaying)
        {
            SetupLines();
            DrawRings(1f, 1f, 1f); // Draw full rings in editor
            
            if (logoTexts != null)
            {
                foreach (var text in logoTexts)
                {
                    if (text != null)
                    {
                        text.characterSpacing = endSpacing;
                        text.alpha = 1f;
                    }
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                timeElapsed = 0f;
            }

            timeElapsed += Time.deltaTime;
            float p1 = GetProgress(0f);
            float p2 = GetProgress(delayBetweenRings);
            float p3 = GetProgress(delayBetweenRings * 2f);
            DrawRings(p1, p2, p3);
            AnimateBloom();
            AnimateText();
        }
    }

    float GetProgress(float delay)
    {
        float t = Mathf.Clamp01((timeElapsed - delay) / animationDuration);
        return EaseInOutCubic(t);
    }

    float EaseInOutCubic(float x)
    {
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    void SetupLines()
    {
        // Initialize random state
        Random.InitState(seed);

        SetupRingLines("Ring1", material1, ring1Streams);
        SetupRingLines("Ring2", material2, ring2Streams);
        SetupRingLines("Ring3", material3, ring3Streams);
    }

    void SetupRingLines(string baseName, Material mat, List<StreamData> streamsList)
    {
        Transform container = transform.Find(baseName);
        if (container == null)
        {
            container = new GameObject(baseName).transform;
            container.SetParent(transform, false);
        }

        // Ensure correct number of children
        // Remove excess
        while (container.childCount > streamsPerRing)
        {
            DestroyImmediate(container.GetChild(container.childCount - 1).gameObject);
        }
        // Add missing
        while (container.childCount < streamsPerRing)
        {
            GameObject go = new GameObject("Stream");
            go.transform.SetParent(container, false);
            go.AddComponent<LineRenderer>();
        }

        streamsList.Clear();
        Color matColor = mat.color;

        for (int i = 0; i < streamsPerRing; i++)
        {
            LineRenderer lr = container.GetChild(i).GetComponent<LineRenderer>();
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 8;
            lr.numCornerVertices = 8;

            StreamData data = new StreamData();
            data.lineRenderer = lr;
            data.spreadMultiplier = Random.Range(0.5f, 1.5f); // Random spread
            
            if (useGradient && startColorGradient != null)
            {
                data.startColor = startColorGradient.Evaluate(Random.value);
            }
            else
            {
                data.startColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f); // Random bright color
            }

            data.targetColor = matColor;
            data.mpb = new MaterialPropertyBlock();
            
            streamsList.Add(data);
        }
    }

    void DrawRings(float p1, float p2, float p3)
    {
        Quaternion rot = Quaternion.Euler(ringRotation);
        //float radius = 1.5f; // 3 unit diameter

        DrawRingGroup(ring1Streams, radius, -ringDistance, rot, p1);
        DrawRingGroup(ring2Streams, radius, 0f, rot, p2);
        DrawRingGroup(ring3Streams, radius, ringDistance, rot, p3);
    }

    void DrawRingGroup(List<StreamData> streams, float radius, float offset, Quaternion rotation, float progress)
    {
        for (int i = 0; i < streams.Count; i++)
        {
            float centeredT = 0f;
            if (streams.Count > 1)
            {
                centeredT = (i - (streams.Count - 1) / 2f); // e.g. -1, 0, 1
            }
            DrawStream(streams[i], radius, offset, rotation, progress, centeredT);
        }
    }

    void DrawStream(StreamData data, float radius, float offset, Quaternion rotation, float progress, float streamOffsetFactor)
    {
        LineRenderer lr = data.lineRenderer;
        if (lr == null) return;

        // Update Color
        Color currentColor = Color.Lerp(data.startColor, data.targetColor, progress);
        data.mpb.SetColor("_BaseColor", currentColor);
        data.mpb.SetColor("_Color", currentColor);
        lr.SetPropertyBlock(data.mpb);

        // Make streams taper from start to end
        // Blend start thickness to end thickness as we approach the end to ensure seamless loop
        float blendFactor = Mathf.Clamp01((progress - 0.8f) / 0.2f);
        float currentStartThickness = Mathf.Lerp(startThickness, endThickness, blendFactor);
        
        lr.startWidth = currentStartThickness;
        lr.endWidth = endThickness;

        // If complete, draw full loop
        if (progress >= 1f)
        {
            lr.loop = true;
            lr.positionCount = segments;
            Vector3[] positions = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = -Mathf.Deg2Rad * (i * 360f / segments);
                
                float spread = streamSpreadEnd * data.spreadMultiplier;
                float r = radius + streamOffsetFactor * spread;
                float y = offset + streamOffsetFactor * spread * 0.5f;

                Vector3 pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                positions[i] = rotation * pos;
            }
            lr.SetPositions(positions);
            return;
        }

        lr.loop = false;
        if (progress <= 0.001f)
        {
            lr.positionCount = 0;
            return;
        }

        float circumference = 2f * Mathf.PI * radius;
        float overlap = circumference * 0.25f; // 90 degrees overlap
        float totalLen = flyInDistance + circumference + overlap;
        
        float headDist = progress * totalLen;
        
        // Target length should ensure we cover the full circle + overlap at the end
        float targetLength = circumference + overlap;
        float currentLength = Mathf.Lerp(flyInDistance * 0.6f, targetLength, progress);
        float tailDist = headDist - currentLength;

        List<Vector3> points = new List<Vector3>();

        // 1. Line Segment Part (Fly-in)
        if (tailDist < flyInDistance)
        {
            float zStart = flyInDistance - tailDist;
            if (zStart > flyInDistance) zStart = flyInDistance;

            float zEnd = (headDist < flyInDistance) ? (flyInDistance - headDist) : 0f;

            int flyInSteps = 20;
            float zDist = zStart - zEnd;
            
            if (zDist > 0.01f)
            {
                for (int i = 0; i <= flyInSteps; i++)
                {
                    float t = (float)i / flyInSteps;
                    float z = Mathf.Lerp(zStart, zEnd, t);
                    
                    float distFactor = Mathf.Clamp01(z / flyInDistance);
                    float spreadCurve = distFactor * distFactor; 
                    
                    float currentSpread = Mathf.Lerp(streamSpreadEnd * data.spreadMultiplier, streamSpreadStart * data.spreadMultiplier, spreadCurve);
                    
                    float r = radius + streamOffsetFactor * currentSpread;
                    float y = offset + streamOffsetFactor * currentSpread * 0.5f;

                    points.Add(rotation * new Vector3(r, y, z));
                }
            }
        }

        // 2. Circle Segment Part
        if (headDist > flyInDistance)
        {
            float startDistOnCircle = Mathf.Max(0f, tailDist - flyInDistance);
            float endDistOnCircle = headDist - flyInDistance;
            
            float startAngleVal = startDistOnCircle / radius;
            float endAngleVal = endDistOnCircle / radius;
            
            float angleStep = (2f * Mathf.PI) / segments;
            
            if (points.Count == 0)
            {
                AddCirclePoint(points, startAngleVal, radius, offset, rotation, streamOffsetFactor, data);
            }
            
            int startStep = Mathf.CeilToInt(startAngleVal / angleStep);
            int endStep = Mathf.FloorToInt(endAngleVal / angleStep);
            
            for (int i = startStep; i <= endStep; i++)
            {
                float a = i * angleStep;
                AddCirclePoint(points, a, radius, offset, rotation, streamOffsetFactor, data);
            }
            
            AddCirclePoint(points, endAngleVal, radius, offset, rotation, streamOffsetFactor, data);
        }
        
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }

    void AddCirclePoint(List<Vector3> points, float angleVal, float radius, float offset, Quaternion rotation, float streamOffsetFactor, StreamData data)
    {
        float angle = -angleVal; // Clockwise
        float spread = streamSpreadEnd * data.spreadMultiplier;
        float r = radius + streamOffsetFactor * spread;
        float y = offset + streamOffsetFactor * spread * 0.5f;
        Vector3 pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        points.Add(rotation * pos);
    }

    void SetupBloom()
    {
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out bloom);
        }
    }

    void AnimateBloom()
    {
        if (bloom == null) return;

        float totalDuration = animationDuration + (delayBetweenRings * 2f);
        float t = Mathf.Clamp01(timeElapsed / totalDuration);
        float curve = EaseInOutCubic(t);

        bloom.threshold.value = Mathf.Lerp(startThreshold, endThreshold, curve);
        bloom.intensity.value = Mathf.Lerp(startIntensity, endIntensity, curve);
        bloom.scatter.value = Mathf.Lerp(startScatter, endScatter, curve);
    }

    void AnimateText()
    {
        if (logoTexts == null) return;

        float t = Mathf.Clamp01((timeElapsed - textDelay) / textAnimationDuration);
        float curve = EaseInOutCubic(t);

        foreach (var text in logoTexts)
        {
            if (text != null)
            {
                text.characterSpacing = Mathf.Lerp(startSpacing, endSpacing, curve);
                text.alpha = Mathf.Lerp(0f, 1f, curve);
            }
        }
    }
}
