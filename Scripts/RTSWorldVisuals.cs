using UnityEngine;

public class RTSWorldVisuals : MonoBehaviour
{
    [Header("Health Bar")]
    public bool showHealthWhenSelected = true;
    public bool showHealthWhenDamaged = true;
    public Vector3 healthBarOffset = new Vector3(0f, 2f, 0f);
    public float healthBarWidth = 1.4f;
    public float healthBarHeight = 0.12f;

    [Header("Selection Circle")]
    public bool showSelectionCircle = true;
    public float selectionCircleRadius = 1.1f;
    public float selectionCircleHeightOffset = 0.04f;

    [Header("Colors")]
    public Color healthBackColor = Color.black;
    public Color healthFillColor = Color.green;
    public Color selectionColor = Color.yellow;
    public Color buildingSelectionColor = Color.cyan;

    private RTSUnit unit;
    private SelectableUnit selectableUnit;
    private SelectableBuilding selectableBuilding;

    private GameObject healthRoot;
    private GameObject healthBack;
    private GameObject healthFill;
    private GameObject selectionCircle;

    private Renderer healthBackRenderer;
    private Renderer healthFillRenderer;
    private Renderer selectionRenderer;

    private Camera mainCamera;

    private void Awake()
    {
        unit = GetComponent<RTSUnit>();
        selectableUnit = GetComponent<SelectableUnit>();
        selectableBuilding = GetComponent<SelectableBuilding>();

        mainCamera = Camera.main;

        CreateHealthBar();
        CreateSelectionCircle();
    }

    private void LateUpdate()
    {
        if (unit == null)
            return;

        UpdateHealthBar();
        UpdateSelectionCircle();
    }

    private void CreateHealthBar()
    {
        healthRoot = new GameObject("HealthBar");
        healthRoot.transform.SetParent(transform);
        healthRoot.transform.localPosition = healthBarOffset;

        healthBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        healthBack.name = "HealthBar_Back";
        healthBack.transform.SetParent(healthRoot.transform);
        healthBack.transform.localPosition = Vector3.zero;
        healthBack.transform.localScale = new Vector3(healthBarWidth, healthBarHeight, 0.04f);

        healthFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        healthFill.name = "HealthBar_Fill";
        healthFill.transform.SetParent(healthRoot.transform);
        healthFill.transform.localPosition = new Vector3(0f, 0f, -0.03f);
        healthFill.transform.localScale = new Vector3(healthBarWidth, healthBarHeight * 0.8f, 0.05f);

        RemoveCollider(healthBack);
        RemoveCollider(healthFill);

        healthBackRenderer = healthBack.GetComponent<Renderer>();
        healthFillRenderer = healthFill.GetComponent<Renderer>();

        healthBackRenderer.material = CreateOpaqueMaterial(healthBackColor);
        healthFillRenderer.material = CreateOpaqueMaterial(healthFillColor);

        healthRoot.SetActive(false);
    }

    private void CreateSelectionCircle()
    {
        if (!showSelectionCircle)
            return;

        selectionCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        selectionCircle.name = "SelectionCircle";
        selectionCircle.transform.SetParent(transform);
        selectionCircle.transform.localPosition = new Vector3(0f, selectionCircleHeightOffset, 0f);
        selectionCircle.transform.localScale = Vector3.one; 

        RemoveCollider(selectionCircle);

        selectionRenderer = selectionCircle.GetComponent<Renderer>();

        Color circleColor = selectableBuilding != null ? buildingSelectionColor : selectionColor;
        selectionRenderer.material = CreateTransparentMaterial(circleColor);

        selectionCircle.SetActive(false);
    }

    private void UpdateHealthBar()
    {
        bool selected = IsSelected();
        bool damaged = unit.currentHp < unit.maxHp;

        bool shouldShow =
            (showHealthWhenSelected && selected) ||
            (showHealthWhenDamaged && damaged);

        healthRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        float hpPercent = unit.maxHp > 0
            ? Mathf.Clamp01((float)unit.currentHp / unit.maxHp)
            : 0f;

        float fillWidth = healthBarWidth * hpPercent;

        healthFill.transform.localScale = new Vector3(
            fillWidth,
            healthBarHeight * 0.8f,
            0.05f
        );

        float xOffset = -(healthBarWidth - fillWidth) * 0.5f;

        healthFill.transform.localPosition = new Vector3(
            xOffset,
            0f,
            -0.03f
        );

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            Vector3 direction = healthRoot.transform.position - mainCamera.transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                healthRoot.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private void UpdateSelectionCircle()
    {
        if (selectionCircle == null)
            return;

        bool selected = IsSelected();
        selectionCircle.SetActive(selected);

        if (!selected)
            return;

        selectionCircle.transform.localPosition = new Vector3(
            0f,
            selectionCircleHeightOffset,
            0f
        );

        Vector3 parentScale = transform.lossyScale;

        float safeX = Mathf.Abs(parentScale.x) > 0.001f ? parentScale.x : 1f;
        float safeY = Mathf.Abs(parentScale.y) > 0.001f ? parentScale.y : 1f;
        float safeZ = Mathf.Abs(parentScale.z) > 0.001f ? parentScale.z : 1f;

        selectionCircle.transform.localScale = new Vector3(
            selectionCircleRadius / safeX,
            0.025f / safeY,
            selectionCircleRadius / safeZ
        );

        if (selectionRenderer != null)
        {
            Color circleColor = selectableBuilding != null ? buildingSelectionColor : selectionColor;
            Color transparentColor = circleColor;
            transparentColor.a = Mathf.Clamp01(transparentColor.a);
            selectionRenderer.material.color = transparentColor;
        }
    }

    private bool IsSelected()
    {
        if (selectableUnit != null && selectableUnit.IsSelected)
            return true;

        if (selectableBuilding != null && selectableBuilding.IsSelected)
            return true;

        return false;
    }

    private Material CreateOpaqueMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Standard"));
        color.a = 1f;
        material.color = color;
        return material;
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Standard"));

        color.a = Mathf.Clamp01(color.a);
        material.color = color;

        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;

        return material;
    }

    private void RemoveCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }
    }
}