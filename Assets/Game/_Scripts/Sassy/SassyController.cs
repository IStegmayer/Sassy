using System;
using System.Security;
using UnityEngine;
using UnityEngine.InputSystem;
using Game._Scripts.Sassy;

namespace Game._Scripts.Sassy
{
    public class SassyController : MonoBehaviour
    {
        // Public for external hooks
        public Vector3 Velocity { get; private set; }

        [SerializeField] private int _speed;
        [SerializeField] private float _maxLaunchSpeed;
        [SerializeField] private float _maxChargeTime;
        [SerializeField] private float _ricoBoost;
        [SerializeField] private float _drag;

        private float _turnSmoothTime = 0.05f;
        private float _turnSmoothVelocity;
        private Rigidbody _rigidbody;
        private Animator _animator;
        private float _startChargeTime;
        private float _chargeTime;
        private float _currentSpeed;
        private float _scale = Constants.BASE_SCALE;
        private bool _collided;
        private Vector2 _launchDirection;
        private int _id;
        private Vector3 _lastPosition;

        private FrameInput _input;

        // TODO: this is the best way?
        private GameObject _meshesContainer;

        private enum PlayerStates
        {
            Launching,
            Charging,
            Slashing,
            Walking,
            Idle,
            Stunned
        }

        private PlayerStates _playerState = PlayerStates.Idle;

        private string _animState = AnimStates.idle;
        private bool _crashed;
        private SassyAnimator _sassyAnimator;

        private static class Constants
        {
            public const float BASE_SCALE = 0.17f;
        }

        void Start() {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _sassyAnimator = GetComponent<SassyAnimator>();
            // TODO: this is the best way?
            _meshesContainer = transform.Find("Meshes").gameObject;
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        private void FixedUpdate() { }

        void Update() {
            if (GameManager.Instance.State != GameState.Playing) return;

            // Calculate velocity
            Velocity = (transform.position - _lastPosition) / Time.deltaTime;
            _lastPosition = transform.position;

            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);

            // Raycasts and Collisions

            CalculateCollision();

            // Calculate Crash
            Crash();
            // Calculate Charge
            Charge();
            // Calculate Launch
            Launch();
            // Calculate Slash
            WindSlash();
            // Calculate Walk
            Walk();

            // Execute the combined movement
            MoveSassy();
        }

        private void CalculateCollision() {
            
            
            if (_playerState == PlayerStates.Launching && !_collided) {
                _currentSpeed = _speed * 3.0f;
                _crashed = true;
                if (_crashed) Invoke(nameof(RestartCrash), 0.5f);
                AudioSystem.Instance.PlaySound("crash", transform.position);
                SetPlayerState(PlayerStates.Stunned, AnimStates.stunned);
            }

            _collided = true;

            // void OnCollisionExit(Collision collision) {
            //     _collided = false;
            // }
        }

        #region GatherInput

        public void OnMove(InputAction.CallbackContext context) =>
            _input.MovementXZ = context.ReadValue<Vector2>();

        public void OnWindslash(InputAction.CallbackContext context) =>
            _input.WindSlash = context.action.WasPressedThisFrame();

        public void OnLaunch(InputAction.CallbackContext context) {
            _input.ChargeDown = context.action.WasPressedThisFrame();
            _input.ChargeUp = context.action.WasReleasedThisFrame();
        }

        #endregion

        #region Crash

        private void Crash() {
            if (_playerState == PlayerStates.Stunned && _currentSpeed >= 0.0f) {
                _currentSpeed -= _drag;
                var position = _rigidbody.position;
                var backwards = -transform.forward;
                position.x += backwards.x * _currentSpeed * Time.deltaTime;
                position.z += backwards.z * _currentSpeed * Time.deltaTime;
                _rigidbody.MovePosition(position);
            }
        }

        private void RestartCrash() {
            _crashed = false;
            _chargeTime = 0.0f;
            _scale = Constants.BASE_SCALE;
            transform.localScale = new Vector3(_scale, _scale, _scale);
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        #endregion

        #region Charge

        private void Charge() {
            if (_input.ChargeDown) {
                _startChargeTime = Time.time;
                SetPlayerState(PlayerStates.Charging, AnimStates.charging);

                AudioSystem.Instance.PlaySound("charge", transform.position);
                _rigidbody.velocity = Vector2.zero;
                _input.ChargeDown = false;
            }

            if (_input.ChargeUp && _playerState == PlayerStates.Charging) {
                _chargeTime = Time.time - _startChargeTime;
                //max launch 3 sec
                if (_chargeTime > _maxChargeTime) {
                    _chargeTime = _maxChargeTime;
                }

                _currentSpeed = (_chargeTime / _maxChargeTime) * _maxLaunchSpeed;
                _launchDirection = _input.MovementXZ;
                SetPlayerState(PlayerStates.Launching, AnimStates.launching);
                AudioSystem.Instance.PlaySound("launch", transform.position);
                _input.ChargeUp = false;
            }

            if (_playerState == PlayerStates.Charging) {
                if (_input.MovementXZ.magnitude >= 0.1f) {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_input.MovementXZ.x, _input.MovementXZ.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity,
                        _turnSmoothTime);
                    _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                }

                if (0.07 < _scale) {
                    _scale = Constants.BASE_SCALE - ((Time.time - _startChargeTime) / _maxChargeTime);
                    transform.localScale = new Vector3(_scale, _scale, _scale);
                }
            }
        }

        #endregion

        #region Launch

        void Launch() {
            if (_currentSpeed >= 0.0f && !_crashed) {
                _currentSpeed -= _drag;
                var pos = transform.position;
                _rigidbody.MovePosition(new Vector3(pos.x + _launchDirection.x * _currentSpeed * Time.deltaTime,
                    pos.y, pos.z + _launchDirection.y * _currentSpeed * Time.deltaTime));
                _startChargeTime = 0.0f;
            }
            else {
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
                _chargeTime = 0.0f;
                _scale = Constants.BASE_SCALE;
                transform.localScale = new Vector3(_scale, _scale, _scale);
                SetPlayerState(PlayerStates.Idle, AnimStates.idle);
            }
        }

        public void ResetLaunch() {
            SetPlayerState(PlayerStates.Launching, AnimStates.slashing);
            transform.localScale = new Vector3(_scale, _scale, _scale);
            Invoke(nameof(EndWindSlash), 0.5f);
        }

        #endregion

        #region WindSlash

        void WindSlash() {
            if (_input.WindSlash) {
                AudioSystem.Instance.PlaySound("windSlash", transform.position);
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.TwinBlade);
                SetPlayerState(PlayerStates.Slashing, AnimStates.slashing);
                transform.localScale = new Vector3(Constants.BASE_SCALE, Constants.BASE_SCALE, Constants.BASE_SCALE);
                Invoke(nameof(EndWindSlash), 0.5f);
                _input.WindSlash = false;
            }

            if (!_collided) {
                // if (collision.gameObject.CompareTag("Wall")) {
                //     AudioSystem.Instance.PlaySound("good", transform.position);
                //     _launchDirection *= -1.0f;
                //     _currentSpeed += _ricoBoost;
                // }

                ResetLaunch();
            }
        }

        public void EndWindSlash() {
            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
            SetPlayerState(PlayerStates.Launching, AnimStates.launching);
        }

        #endregion

        #region Walk

        private void Walk() {
            if ((_playerState == PlayerStates.Idle || _playerState == PlayerStates.Walking) && !_crashed) {
                if (_input.MovementXZ.magnitude >= 0.1f) {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_input.MovementXZ.x, _input.MovementXZ.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity,
                        _turnSmoothTime);
                    _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                }

                var position = _rigidbody.position;
                position.x += _input.MovementXZ.x * _speed * Time.deltaTime;
                position.z += _input.MovementXZ.y * _speed * Time.deltaTime;
                _rigidbody.MovePosition(position);
                SetPlayerState(PlayerStates.Walking, AnimStates.walking);
            }
        }

        #endregion

        private void MoveSassy() { }

        void SetPlayerState(PlayerStates newPlayerState, string animState) {
            _animator.Play(animState);
            _playerState = newPlayerState;
            _animState = animState;
        }
    }
}