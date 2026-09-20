document.getElementById('t').textContent = 'Zaman: ' + new Date().toLocaleString('tr-TR');
document.getElementById('btn').onclick = () => {
  document.getElementById('t').textContent = 'Çalışıyor ✓ ' + new Date().toLocaleTimeString('tr-TR');
};
