using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game._Scripts.Sassy;

namespace Game._Scripts.Sassy
{
    public class SassyController : MonoBehaviour
    {
        [SerializeField] private int _speed;
        [SerializeField] private float _maxLaunchSpeed;
        [SerializeField] private float _maxChargeTime;
        [SerializeField] private float _ricoBoost;
        [SerializeField] private float _drag;

        private float _turnSmoothTime = 0.05f;
        private float turnSmoothVelocity;
        private Vector2 _movementInput = Vector2.zero;
        private Rigidbody _rigidbody;
        private Animator _animator;
        private float _startChargeTime = 0;
        private float _chargeTime = 0;
        private float _currentSpeed = 0;
        private float _scale = Constants.BASE_SCALE;
        private bool _keyDown = false;
        private bool _keyUp = false;
        private bool _windslashDown = false;
        private Vector2 _launchDirection;

        private int _id;

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

        private static class AnimStates
        {
            public static readonly string idle = "Idle";
            public static readonly string walking = "Walking";
            public static readonly string charging = "Charging";
            public static readonly string launching = "Launching";
            public static readonly string slashing = "Slashing";
            public static readonly string stunned = "Stunned";
        }

        private string _animState = AnimStates.idle;
        private bool _crashed;
        private SassyAnimator _sassyAnimator;

        private static class Constants
        {
            public const float BASE_SCALE = 0.17f;
        }

        void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _sassyAnimator = GetComponent<SassyAnimator>();
            // TODO: this is the best way?
            _meshesContainer = transform.Find("Meshes").gameObject;
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        void Update()
        {
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance.State != GameState.Playing) return;
            if (_windslashDown)
            {
                WindSlash();
                _windslashDown = false;
            }

            if (_keyDown)
            {
                _startChargeTime = Time.time;
                SetPlayerState(PlayerStates.Charging, AnimStates.charging);

                AudioSystem.Instance.PlaySound("charge", transform.position);
                _rigidbody.velocity = Vector2.zero;
                _keyDown = false;
            }

            if (_keyUp && _playerState == PlayerStates.Charging)
            {
                _chargeTime = Time.time - _startChargeTime;
                //max launch 3 sec
                if (_chargeTime > _maxChargeTime)
                {
                    _chargeTime = _maxChargeTime;
                }

                _currentSpeed = (_chargeTime / _maxChargeTime) * _maxLaunchSpeed;
                _launchDirection = _movementInput;
                SetPlayerState(PlayerStates.Launching, AnimStates.launching);
                AudioSystem.Instance.PlaySound("launch", transform.position);
                _keyUp = false;
            }

            if (_playerState == PlayerStates.Charging)
            {
                if (0.07 < _scale)
                {
                    _scale = Constants.BASE_SCALE - ((Time.time - _startChargeTime) / _maxChargeTime);
                    transform.localScale = new Vector3(_scale, _scale, _scale);
                }
            }

            if (_playerState == PlayerStates.Stunned && _currentSpeed >= 0.0f)
            {
                _currentSpeed -= _drag;
                var position = _rigidbody.position;
                var backwards = transform.forward * -1.0f;
                position.x += backwards.x * _currentSpeed * Time.deltaTime;
                position.z += backwards.z * _currentSpeed * Time.deltaTime;
                _rigidbody.MovePosition(position);
            }

            if (_playerState == PlayerStates.Launching || _playerState == PlayerStates.Slashing)
            {
                Launch();
            }
            else if ((_playerState == PlayerStates.Idle || _playerState == PlayerStates.Walking) && !_crashed)
            {
                if (_movementInput.magnitude >= 0.1f)
                {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_movementInput.x, _movementInput.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity,
                        _turnSmoothTime);
                    _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                    SetPlayerState(PlayerStates.Walking, AnimStates.walking);
                }

                var position = _rigidbody.position;
                position.x += _movementInput.x * _speed * Time.deltaTime;
                position.z += _movementInput.y * _speed * Time.deltaTime;
                _rigidbody.MovePosition(position);
            }
        }

        private void RestartCrash()
        {
            _crashed = false;
            _chargeTime = 0.0f;
            _scale = Constants.BASE_SCALE;
            transform.localScale = new Vector3(_scale, _scale, _scale);
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            _movementInput = context.ReadValue<Vector2>();
        }

        public void OnWindslash(InputAction.CallbackContext context)
        {
            if (_playerState == PlayerStates.Launching && context.action.WasPressedThisFrame())
                _windslashDown = true;
        }

        public void OnLaunch(InputAction.CallbackContext context)
        {
            if (context.action.WasPressedThisFrame() &&
                (_playerState == PlayerStates.Walking || _playerState == PlayerStates.Idle))
                _keyDown = true;
            if (context.action.WasReleasedThisFrame() && _playerState == PlayerStates.Charging)
                _keyUp = true;
        }

        void Launch()
        {
            if (_currentSpeed >= 0.0f && !_crashed)
            {
                _currentSpeed -= _drag;
                var pos = transform.position;
                _rigidbody.MovePosition(new Vector3(pos.x + _launchDirection.x * _currentSpeed * Time.deltaTime,
                    pos.y, pos.z + _launchDirection.y * _currentSpeed * Time.deltaTime));
                _startChargeTime = 0.0f;
            }
            else
            {
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
                _chargeTime = 0.0f;
                _scale = Constants.BASE_SCALE;
                transform.localScale = new Vector3(_scale, _scale, _scale);
                SetPlayerState(PlayerStates.Idle, AnimStates.idle);
            }
        }

        void WindSlash()
        {
            AudioSystem.Instance.PlaySound("windSlash", transform.position);
            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.TwinBlade);
            SetPlayerState(PlayerStates.Slashing, AnimStates.slashing);
            transform.localScale = new Vector3(Constants.BASE_SCALE, Constants.BASE_SCALE, Constants.BASE_SCALE);
        }

        public void EndWindSlash()
        {
            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
            SetPlayerState(PlayerStates.Launching, AnimStates.launching);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_playerState == PlayerStates.Slashing)
            {
                if (collision.gameObject.CompareTag("Wall"))
                {
                    AudioSystem.Instance.PlaySound("good", transform.position);
                    _launchDirection *= -1.0f;
                    _currentSpeed += _ricoBoost;
                }

                ResetLaunch();
            }
            // else if (_playerState == PlayerStates.Launching)
            // {
            //     _currentSpeed = _speed * 3.0f;
            //     _crashed = true;
            //     if (_crashed) Invoke(nameof(RestartCrash), 0.5f);
            //     AudioSystem.Instance.PlaySound("crash", transform.position);
            //     SetPlayerState(PlayerStates.Stunned, AnimStates.stunned);
            // }
        }

        public void ResetLaunch()
        {
            SetPlayerState(PlayerStates.Launching, AnimStates.slashing);
            transform.localScale = new Vector3(_scale, _scale, _scale);
            Invoke(nameof(EndWindSlash), 0.5f);
        }

        void SetPlayerState(PlayerStates newPlayerState, string animState)
        {
            // _animator.SetInteger("CurrentState", (int) animState);
            _animator.Play(animState);
            _playerState = newPlayerState;
            _animState = animState;
        }
    }
}