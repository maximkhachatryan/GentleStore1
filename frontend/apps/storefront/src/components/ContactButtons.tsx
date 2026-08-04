import { useTranslation } from 'react-i18next';
import { callLink, whatsappLink } from '../lib/contact';

interface Props {
  phone: string;
  message: string;
  size?: 'md' | 'lg';
}

function WhatsAppIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden="true">
      <path d="M17.5 14.4c-.3-.1-1.7-.8-1.9-.9-.3-.1-.4-.1-.6.1-.2.3-.7.9-.8 1-.2.2-.3.2-.6.1-1.5-.7-2.4-1.3-3.4-3-.3-.4.3-.4.7-1.3.1-.2 0-.4 0-.5s-.6-1.5-.9-2c-.2-.5-.4-.5-.6-.5h-.5c-.2 0-.5.1-.7.3-.3.3-1 .9-1 2.3s1 2.7 1.2 2.9c.1.2 2 3.1 5 4.3 1.8.7 2.5.8 3.4.7.5-.1 1.7-.7 1.9-1.4.2-.6.2-1.2.2-1.4-.1-.1-.3-.2-.6-.3z" />
      <path d="M12 2a10 10 0 0 0-8.6 15l-1.3 4.7 4.8-1.3A10 10 0 1 0 12 2zm0 18.2c-1.5 0-3-.4-4.3-1.2l-.3-.2-2.9.8.8-2.8-.2-.3A8.2 8.2 0 1 1 12 20.2z" />
    </svg>
  );
}

function PhoneIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M2.5 5.5c0-1 .8-1.8 1.8-1.8h2c.8 0 1.5.6 1.7 1.4l.8 3c.1.6-.1 1.2-.5 1.6l-1.3 1.3a12 12 0 0 0 5 5l1.3-1.3c.4-.4 1-.6 1.6-.5l3 .8c.8.2 1.4.9 1.4 1.7v2c0 1-.8 1.8-1.8 1.8A16.5 16.5 0 0 1 2.5 5.5z"
      />
    </svg>
  );
}

export default function ContactButtons({ phone, message, size = 'md' }: Props) {
  const { t } = useTranslation();
  const pad = size === 'lg' ? 'px-5 py-3 text-base' : 'px-4 py-2.5 text-sm';

  return (
    <div className="flex gap-3">
      <a
        href={whatsappLink(phone, message)}
        target="_blank"
        rel="noreferrer"
        className={`inline-flex flex-1 items-center justify-center gap-2 rounded-xl bg-emerald-600 font-semibold text-white transition hover:bg-emerald-700 ${pad}`}
      >
        <WhatsAppIcon /> {t('contact.whatsapp')}
      </a>
      <a
        href={callLink(phone)}
        className={`inline-flex items-center justify-center gap-2 rounded-xl border border-slate-300 bg-white font-semibold text-slate-700 transition hover:bg-slate-50 ${pad}`}
      >
        <PhoneIcon /> {t('contact.call')}
      </a>
    </div>
  );
}
