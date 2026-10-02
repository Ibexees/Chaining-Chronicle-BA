using UnityEngine;

namespace CC.Game.Stage
{
    /// <summary>Orthographic camera that follows a target smoothly. Put on the Main Camera of the Stage scene.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class StageCameraFollow : MonoBehaviour
    {
        [SerializeField] private float orthographicSize = 6f;
        [SerializeField] private float smoothTime = 0.08f;
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.08f);

        private Transform _target;
        private Vector3 _velocity;

        private void Awake()
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        public void SetTarget(Transform target, bool snap)
        {
            _target = target;
            if (snap && target != null) transform.position = Goal();
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, Goal(), ref _velocity, smoothTime);
        }

        private Vector3 Goal() => new Vector3(_target.position.x, _target.position.y, -10f);
    }
}
