// Функции авторизации и регистрации пользователей

document.addEventListener('DOMContentLoaded', function () {
    // Если на странице есть форма входа
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    }

    // Если на странице есть форма регистрации
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', handleRegister);
    }
});

async function handleLogin(e) {
    e.preventDefault();

    const form = e.target;
    const email = form.email.value.trim();
    const password = form.password.value;
    const rememberMe = form.rememberMe.checked;

    // Валидация
    if (!Utils.isValidEmail(email)) {
        Utils.showError('emailError', 'Введите корректный email');
        return;
    }

    if (password.length < 6) {
        Utils.showError('passwordError', 'Пароль должен содержать не менее 6 символов');
        return;
    }

    const loginBtn = document.getElementById('loginBtn');
    Utils.showLoading(loginBtn);

    try {
        // Отправляем запрос на API
        const response = await ApiClient.login(email, password);

        if (response.success) {
            const { accessToken, refreshToken, userId, email: userEmail, role, name } = response.data;

            // Сохраняем токен и данные пользователя
            Utils.saveToken(accessToken);
            Utils.saveUser({
                id: userId,
                email: userEmail,
                role: role,
                name: name
            });

            // Если выбрано "Запомнить меня", сохраняем refresh token
            if (rememberMe) {
                localStorage.setItem('refreshToken', refreshToken);
            }

            Utils.showNotification('Вы успешно вошли!', 'success');

            // Перенаправляем на главную страницу
            setTimeout(() => {
                window.location.href = 'menu.html';
            }, 1500);

        } else {
            Utils.showError('emailError', response.message || 'Неверный email или пароль');
        }
    } catch (error) {
        console.error('Login error:', error);
        Utils.showError('emailError', error.message || 'Ошибка при входе. Пожалуйста попробуйте снова.');
    } finally {
        Utils.hideLoading(loginBtn);
    }
}

async function handleRegister(e) {
    e.preventDefault();

    const form = e.target;
    const firstName = form.firstName.value.trim();
    const lastName = form.lastName.value.trim();
    const email = form.email.value.trim();
    const phone = form.phone.value.trim();
    const password = form.password.value;
    const confirmPassword = form.confirmPassword.value;

    // Валидация
    const errors = [];

    if (!firstName) {
        errors.push({ field: 'firstNameError', message: 'Введите имя' });
    }

    if (!lastName) {
        errors.push({ field: 'lastNameError', message: 'Введите фамилию' });
    }

    if (!Utils.isValidEmail(email)) {
        errors.push({ field: 'emailError', message: 'Введите корректный email' });
    }

    if (phone && !Utils.isValidPhone(phone)) {
        errors.push({ field: 'phoneError', message: 'Введите корректный номер телефона' });
    }

    if (password.length < 6) {
        errors.push({ field: 'passwordError', message: 'Пароль должен содержать не менее 6 символов' });
    }

    if (password !== confirmPassword) {
        errors.push({ field: 'confirmPasswordError', message: 'Пароли не совпадают' });
    }

    if (!form.terms.checked) {
        alert('Необходимо принять условия соглашения');
        return;
    }

    if (errors.length > 0) {
        errors.forEach(error => {
            Utils.showError(error.field, error.message);
        });
        return;
    }

    const registerBtn = document.getElementById('registerBtn');
    Utils.showLoading(registerBtn);

    try {
        const userData = {
            email: email,
            password: password,
            phoneNumber: phone || '',
            firstName: firstName,
            lastName: lastName,
            role: 'Customer'
        };

        const response = await ApiClient.register(userData);

        if (response.success) {
            Utils.showNotification('Регистрация успешна! Выполняется вход.', 'success');

            // Автоматический вход после регистрации
            setTimeout(async () => {
                try {
                    const loginResponse = await ApiClient.login(email, password);

                    if (loginResponse.success) {
                        const { accessToken, userId, email: userEmail, role, name } = loginResponse.data;

                        Utils.saveToken(accessToken);
                        Utils.saveUser({
                            id: userId,
                            email: userEmail,
                            role: role,
                            name: name
                        });

                        window.location.href = 'menu.html';
                    }
                } catch (loginError) {
                    window.location.href = 'auth.html';
                }
            }, 2000);

        } else {
            Utils.showError('emailError', response.message || 'Ошибка при регистрации');
        }
    } catch (error) {
        console.error('Registration error:', error);

        if (error.message.includes('already exists')) {
            Utils.showError('emailError', 'Пользователь с таким email уже существует');
        } else {
            Utils.showError('emailError', error.message || 'Ошибка при регистрации');
        }
    } finally {
        Utils.hideLoading(registerBtn);
    }
}

// Функция для выхода (для использования в других файлах)
function logout() {
    ApiClient.logout().catch(console.error);
    Utils.clearAuth();
    window.location.href = 'index.html';
}

// Экспорт для Node.js (если используется)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        handleLogin,
        handleRegister,
        logout
    };
}